using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mizan.Application.Ai;
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
    private const string CoachPrompt = """
        You are Mizan, a nutrition and training assistant. Mizan is Amharic for balance.

        Answer from the user's own log when it is given to you below. When it is
        not, say what you would need rather than guessing at their numbers - the
        user controls what you can see, and an absent section means they chose
        not to share it, not that there is nothing there.

        Be concise and concrete. Prefer one specific suggestion to three vague
        ones. You are not a doctor and you do not diagnose.
        """;

    private const string AnalysisPrompt = """
        Identify the foods in this photo and estimate the portion and macros of
        each. Estimate honestly: a low confidence with a sensible guess is more
        useful than false precision. Return only the declared JSON.
        """;

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

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly IAiProvider _provider;
    private readonly IAiQuotaService _quota;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly ILogger<NutritionAiService> _logger;

    public NutritionAiService(
        IAiProvider provider,
        IAiQuotaService quota,
        IAiContextBuilder contextBuilder,
        ILogger<NutritionAiService> logger)
    {
        _provider = provider;
        _quota = quota;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public async Task<string> GetNutritionAdviceAsync(
        Guid userId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var context = await _contextBuilder.BuildAsync(userId, userId, cancellationToken);

        var messages = new List<AiMessage> { new(AiRole.System, CoachPrompt) };
        if (!context.IsEmpty)
        {
            messages.Add(new AiMessage(AiRole.System, context.Summary));
        }
        messages.Add(new AiMessage(AiRole.User, userMessage));

        var response = await CallAsync(
            userId,
            context.HouseholdId,
            AiFeatures.Chat,
            new AiCompletionRequest { Messages = messages },
            EstimateTokens(userMessage.Length + context.Summary.Length),
            cancellationToken);

        return response.Content;
    }

    public async Task<FoodAnalysisResult> AnalyzeFoodImageAsync(
        Guid userId,
        byte[] imageBytes,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new AiCompletionRequest
        {
            Messages =
            [
                new AiMessage(AiRole.User, AnalysisPrompt, new AiImage(imageBytes, contentType)),
            ],
            ResponseSchema = new AiJsonSchema("food_analysis_v1", AnalysisSchemaV1),
            Temperature = 0.2,
        };

        // An image costs far more prompt tokens than its bytes suggest; a flat
        // reservation that errs high is better than a clever estimate that
        // errs low, because the settle corrects it either way.
        var response = await CallAsync(
            userId, householdId: null, AiFeatures.FoodAnalysis, request, 2_000, cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<FoodAnalysisResult>(response.Content, Json)
                ?? throw new AiUnavailableException("The assistant could not read that photo. Try another.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Food analysis response did not match the declared schema");
            throw new AiUnavailableException("The assistant could not read that photo. Try another.");
        }
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
        CancellationToken cancellationToken)
    {
        if (!_provider.IsConfigured)
        {
            throw new AiUnavailableException("The assistant is not configured on this server.");
        }

        var lease = await _quota.ReserveAsync(
            userId, householdId, feature, estimatedTokens, cancellationToken);

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
