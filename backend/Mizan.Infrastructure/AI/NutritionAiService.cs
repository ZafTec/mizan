using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mizan.Application.Ai;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.AI;

/// <summary>
/// Chat and food-photo analysis, both over the v2 platform.
///
/// What changed in phase 9: this used to build a Semantic Kernel with a plugin
/// whose tools auto-invoked, so the model could write to the food diary
/// unattended. It also called a hardcoded model with no meter and no consent.
/// All three are corrected here - the context is whatever the user has
/// consented to and nothing more, every call is reserved and settled against a
/// quota, and the model no longer writes anything. Tool calling returns in
/// phase 10 behind an allowlist and an explicit confirmation
/// (docs/REFOCUS.md §10).
/// </summary>
public class NutritionAiService : INutritionAiService
{
    /// <summary>
    /// Versioned with the DTO it fills. A response that does not match is a
    /// failed call, not something to scrape with a regex.
    /// </summary>
    private const string AnalysisSchemaV1 = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["foods", "totalCalories", "confidence", "note"],
          "properties": {
            "foods": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["name", "portionGrams", "calories", "protein", "carbs", "fat"],
                "properties": {
                  "name": { "type": "string" },
                  "portionGrams": { "type": "number" },
                  "calories": { "type": "number" },
                  "protein": { "type": "number" },
                  "carbs": { "type": "number" },
                  "fat": { "type": "number" }
                }
              }
            },
            "totalCalories": { "type": "number" },
            "confidence": { "type": "number" },
            "note": { "type": ["string", "null"] }
          }
        }
        """;

    private const string SuggestionSchemaV1 = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["suggestions", "note"],
          "properties": {
            "suggestions": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["title", "description", "calories", "protein", "carbs", "fat", "reason"],
                "properties": {
                  "title": { "type": "string" },
                  "description": { "type": "string" },
                  "calories": { "type": "number" },
                  "protein": { "type": "number" },
                  "carbs": { "type": "number" },
                  "fat": { "type": "number" },
                  "reason": { "type": "string" }
                }
              }
            },
            "note": { "type": ["string", "null"] }
          }
        }
        """;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly IAiProvider _provider;
    private readonly IAiQuotaService _quota;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiPromptResolver _prompts;
    private readonly IAiToolRunner _tools;
    private readonly ILogger<NutritionAiService> _logger;

    public NutritionAiService(
        IAiProvider provider,
        IAiQuotaService quota,
        IAiContextBuilder contextBuilder,
        IAiPromptResolver prompts,
        IAiToolRunner tools,
        ILogger<NutritionAiService> logger)
    {
        _provider = provider;
        _quota = quota;
        _contextBuilder = contextBuilder;
        _prompts = prompts;
        _tools = tools;
        _logger = logger;
    }

    public async Task<AiChatTurn> GetNutritionAdviceAsync(
        Guid userId,
        string userMessage,
        IReadOnlyList<AiChatHistoryTurn> history,
        CancellationToken cancellationToken = default)
    {
        var context = await _contextBuilder.BuildAsync(userId, userId, cancellationToken);
        var prompt = await _prompts.ResolveAsync(AiPromptKeys.Chat, cancellationToken);

        var messages = new List<AiMessage> { new(AiRole.System, prompt.SystemPrompt) };
        if (!context.IsEmpty)
        {
            // After the system prompt and before the history, so the freshest
            // numbers are what the model reasons from rather than whatever it
            // said about them three turns ago.
            messages.Add(new AiMessage(AiRole.System, context.Summary));
        }

        var historyLength = 0;
        foreach (var turn in history)
        {
            messages.Add(new AiMessage(turn.FromUser ? AiRole.User : AiRole.Assistant, turn.Content));
            historyLength += turn.Content.Length;
        }

        messages.Add(new AiMessage(AiRole.User, userMessage));

        // The assistant acts as well as answers now. Every tool is refused by
        // the runner unless this user granted that axis, so offering the whole
        // catalogue here widens what the model may *ask* for, not what it may
        // do (docs/REFOCUS.md §11).
        var specs = AiToolCatalogue.Chat
            .Select(tool => new AiToolSpec(tool.Name, tool.Description, tool.ParametersSchema))
            .ToList();

        var performed = new List<AiToolInvocation>();
        var toolContext = new AiToolContext(userId);

        for (var round = 0; round <= MaxToolRounds; round++)
        {
            var last = round == MaxToolRounds;
            var response = await CallAsync(
                userId,
                context.HouseholdId,
                AiFeatures.Chat,
                new AiCompletionRequest
                {
                    Messages = messages,
                    // The final round offers nothing, so the model has to
                    // answer rather than ask for one more call.
                    Tools = last ? [] : specs,
                },
                EstimateTokens(userMessage.Length + context.Summary.Length + historyLength),
                prompt.VersionId,
                cancellationToken);

            if (response.ToolCalls.Count == 0)
            {
                return new AiChatTurn(response.Content, prompt.VersionId, performed);
            }

            messages.Add(new AiMessage(AiRole.Assistant, response.Content)
            {
                ToolCalls = response.ToolCalls,
            });

            foreach (var call in response.ToolCalls)
            {
                var invocation = await _tools.RunAsync(call, toolContext, cancellationToken);
                performed.Add(invocation);

                messages.Add(new AiMessage(
                    AiRole.Tool,
                    invocation.Succeeded ? invocation.Summary : $"Failed: {invocation.Error}")
                {
                    ToolCallId = call.Id,
                });
            }
        }

        // Unreachable: the last round has no tools, so it cannot ask for one.
        throw new AiUnavailableException("The assistant could not finish that. Try again.");
    }

    public async Task<FoodAnalysisResult> AnalyzeFoodImageAsync(
        Guid userId,
        byte[] imageBytes,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var prompt = await _prompts.ResolveAsync(AiPromptKeys.FoodAnalysis, cancellationToken);

        var request = new AiCompletionRequest
        {
            Messages =
            [
                new AiMessage(AiRole.System, prompt.SystemPrompt),
                new AiMessage(AiRole.User, "Analyse this meal photo.", new AiImage(imageBytes, contentType)),
            ],
            ResponseSchema = new AiJsonSchema("food_analysis_v1", AnalysisSchemaV1),
            Temperature = 0.2,
        };

        // An image costs far more prompt tokens than its bytes suggest; a flat
        // reservation that errs high is better than a clever estimate that
        // errs low, because the settle corrects it either way.
        var response = await CallAsync(
            userId, householdId: null, AiFeatures.FoodAnalysis, request, 2_000, prompt.VersionId, cancellationToken);

        return Parse<FoodAnalysisResult>(
            response.Content, "food analysis", "The assistant could not read that photo. Try another.");
    }

    /// <summary>
    /// A response that does not match its declared schema is a failed call.
    /// There is no regex fallback: scraping a shape out of prose is how a
    /// half-parsed answer ends up looking like a real one (docs/REFOCUS.md §10).
    /// </summary>
    private T Parse<T>(string content, string what, string failure)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content, Json)
                ?? throw new AiUnavailableException(failure);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "The {What} response did not match the declared schema", what);
            throw new AiUnavailableException(failure);
        }
    }

    public async Task<MealSuggestionResult> SuggestMealsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var context = await _contextBuilder.BuildAsync(userId, userId, cancellationToken);
        var prompt = await _prompts.ResolveAsync(AiPromptKeys.Suggestions, cancellationToken);

        var messages = new List<AiMessage> { new(AiRole.System, prompt.SystemPrompt) };
        if (!context.IsEmpty)
        {
            messages.Add(new AiMessage(AiRole.System, context.Summary));
        }
        messages.Add(new AiMessage(AiRole.User, "Propose meals for the rest of today."));

        var response = await CallAsync(
            userId,
            context.HouseholdId,
            AiFeatures.Suggestions,
            new AiCompletionRequest
            {
                Messages = messages,
                ResponseSchema = new AiJsonSchema("meal_suggestions_v1", SuggestionSchemaV1),
                Temperature = 0.4,
            },
            EstimateTokens(context.Summary.Length),
            prompt.VersionId,
            cancellationToken);

        return Parse<MealSuggestionResult>(
            response.Content, "meal suggestions", "The assistant could not put a list together. Try again.");
    }

    /// <summary>
    /// Each round is a metered call, so the ceiling is low. Three is enough for
    /// "record the goal, record the weight, then answer"; a model still looping
    /// after that is stuck, and letting it keep going is how one conversation
    /// spends a day's allowance.
    /// </summary>
    private const int MaxToolRounds = 3;

    public async Task<OnboardingTurn> RunOnboardingTurnAsync(
        Guid userId,
        string userMessage,
        IReadOnlyList<AiChatHistoryTurn> history,
        CancellationToken cancellationToken = default)
    {
        var prompt = await _prompts.ResolveAsync(AiPromptKeys.Onboarding, cancellationToken);
        var specs = AiToolCatalogue.Onboarding
            .Select(tool => new AiToolSpec(tool.Name, tool.Description, tool.ParametersSchema))
            .ToList();

        // No log context here on purpose. Onboarding is where the log gets
        // created; there is nothing to read yet, and asking would mean asking
        // for consent before the user has seen what they are consenting to.
        var messages = new List<AiMessage> { new(AiRole.System, prompt.SystemPrompt) };
        foreach (var turn in history)
        {
            messages.Add(new AiMessage(turn.FromUser ? AiRole.User : AiRole.Assistant, turn.Content));
        }
        messages.Add(new AiMessage(AiRole.User, userMessage));

        var performed = new List<AiToolInvocation>();
        var context = new AiToolContext(userId);

        for (var round = 0; round <= MaxToolRounds; round++)
        {
            var last = round == MaxToolRounds;
            var response = await CallAsync(
                userId,
                householdId: null,
                AiFeatures.Onboarding,
                new AiCompletionRequest
                {
                    Messages = messages,
                    // The final round offers no tools, so the model has to
                    // produce prose rather than asking for one more call.
                    Tools = last ? [] : specs,
                    Temperature = 0.5,
                },
                EstimateTokens(messages.Sum(m => m.Content.Length)),
                prompt.VersionId,
                cancellationToken);

            if (response.ToolCalls.Count == 0)
            {
                return new OnboardingTurn(response.Content, prompt.VersionId, performed);
            }

            messages.Add(new AiMessage(AiRole.Assistant, response.Content)
            {
                ToolCalls = response.ToolCalls,
            });

            foreach (var call in response.ToolCalls)
            {
                var invocation = await _tools.RunAsync(call, context, cancellationToken);
                performed.Add(invocation);

                messages.Add(new AiMessage(
                    AiRole.Tool,
                    invocation.Succeeded ? invocation.Summary : $"Failed: {invocation.Error}")
                {
                    ToolCallId = call.Id,
                });
            }
        }

        // Unreachable: the last round has no tools, so it cannot ask for one.
        throw new AiUnavailableException("The assistant could not finish that. Try again.");
    }

    public async Task<TrainerAnswer> AskAboutClientAsync(
        Guid trainerId,
        Guid clientId,
        string question,
        IReadOnlyList<AiChatHistoryTurn> history,
        CancellationToken cancellationToken = default)
    {
        // Principal and subject differ here, which is the whole point: the
        // builder asks IDataAccessPolicy what this coach may see of this
        // client and receives nothing more.
        var context = await _contextBuilder.BuildAsync(trainerId, clientId, cancellationToken);
        var prompt = await _prompts.ResolveAsync(AiPromptKeys.TrainerClient, cancellationToken);

        var messages = new List<AiMessage> { new(AiRole.System, prompt.SystemPrompt) };
        messages.Add(new AiMessage(
            AiRole.System,
            context.IsEmpty
                ? "This client has shared nothing with you for AI use. Say so and stop."
                : context.Summary));

        var historyLength = 0;
        foreach (var turn in history)
        {
            messages.Add(new AiMessage(turn.FromUser ? AiRole.User : AiRole.Assistant, turn.Content));
            historyLength += turn.Content.Length;
        }

        messages.Add(new AiMessage(AiRole.User, question));

        var response = await CallAsync(
            // Billed to the coach. A client must never be rate-limited by
            // their coach's questions about them (docs/REFOCUS.md §11).
            trainerId,
            householdId: null,
            AiFeatures.TrainerClient,
            // No Tools. A trainer surface is read-only over client data, and
            // the way to guarantee that is to offer nothing to call.
            new AiCompletionRequest { Messages = messages },
            EstimateTokens(question.Length + context.Summary.Length + historyLength),
            prompt.VersionId,
            cancellationToken);

        return new TrainerAnswer(response.Content, prompt.VersionId, context.IncludedAxes);
    }

    /// <summary>
    /// The one path to the provider: reserve, call, settle. Settling happens in
    /// a finally, so a failed or cancelled call still costs what it cost.
    /// </summary>
    private async Task<AiCompletionResponse> CallAsync(
        Guid userId,
        Guid? householdId,
        string feature,
        AiCompletionRequest request,
        int estimatedTokens,
        Guid? promptVersionId,
        CancellationToken cancellationToken)
    {
        if (!_provider.IsConfigured)
        {
            throw new AiUnavailableException("The assistant is not configured on this server.");
        }

        var lease = await _quota.ReserveAsync(
            userId, householdId, feature, estimatedTokens, promptVersionId, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var usage = AiTokenUsage.None;
        var model = _provider.Model;
        var outcome = AiCallOutcome.ProviderError;

        try
        {
            var response = await _provider.CompleteAsync(request, cancellationToken);
            usage = response.Usage;
            model = response.Model;
            outcome = AiCallOutcome.Succeeded;
            return response;
        }
        catch (OperationCanceledException)
        {
            outcome = AiCallOutcome.Timeout;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            // The reservation must be settled even when the caller is gone, so
            // this deliberately does not observe the request's cancellation.
            await _quota.SettleAsync(
                lease, usage, model, (int)stopwatch.ElapsedMilliseconds, outcome, CancellationToken.None);
        }
    }

    /// <summary>Four characters to a token is close enough for a reservation the settle corrects.</summary>
    private static int EstimateTokens(int characters) => Math.Max(256, characters / 4);
}
