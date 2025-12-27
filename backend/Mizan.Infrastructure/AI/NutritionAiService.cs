using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.AI;

public class NutritionAiService : INutritionAiService
{
    private readonly IMizanDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NutritionAiService> _logger;

    private const string SystemPrompt = @"You are Mizan AI, a helpful nutrition and fitness coach.
Your name comes from the Amharic word for 'balance' (ሚዛን), representing the balance between nutrition and fitness.

You help users:
- Track their daily food intake
- Understand nutritional information
- Reach their health and fitness goals
- Suggest recipes that fit their remaining macros
- Provide personalized nutrition advice

Be encouraging, knowledgeable, and culturally aware. When users mention Ethiopian foods,
show familiarity with traditional dishes like injera, doro wat, kitfo, and others.

Always use the available tools to log food, get nutrition info, and provide accurate daily summaries.
If a user mentions eating something, offer to log it for them.";

    public NutritionAiService(IMizanDbContext context, IConfiguration configuration, ILogger<NutritionAiService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetNutritionAdviceAsync(Guid userId, string userMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var kernel = CreateKernel(userId);

            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(SystemPrompt);
            chatHistory.AddUserMessage(userMessage);

            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 1000,
                Temperature = 0.7
            };

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                kernel,
                cancellationToken);

            return response.Content ?? "I apologize, but I couldn't generate a response. Please try again.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting nutrition advice for user {UserId}", userId);
            return "I'm sorry, I encountered an error while processing your request. Please try again later.";
        }
    }

    public async Task<FoodAnalysisResult> AnalyzeFoodImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            var builder = Kernel.CreateBuilder();

            var apiKey = _configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI API key not configured");

            builder.AddOpenAIChatCompletion(
                modelId: "gpt-4o",
                apiKey: apiKey);

            var kernel = builder.Build();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var history = new ChatHistory();
            history.AddSystemMessage(@"Analyze food images and return a JSON response with this exact structure:
{
  ""foods"": [
    {
      ""name"": ""food name"",
      ""portionGrams"": estimated_weight_in_grams,
      ""calories"": estimated_calories,
      ""protein"": estimated_protein_grams,
      ""carbs"": estimated_carbs_grams,
      ""fat"": estimated_fat_grams
    }
  ],
  ""totalCalories"": sum_of_all_calories,
  ""confidence"": confidence_score_0_to_1
}

Be as accurate as possible with portion estimates. If you cannot identify a food, use your best guess based on appearance.");

            var imageContent = new ImageContent(imageBytes, "image/jpeg");
            var messageContent = new ChatMessageContentItemCollection
            {
                new TextContent("Please analyze this meal image and estimate the nutritional content:"),
                imageContent
            };

            history.AddUserMessage(messageContent);

            var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
            var content = response.Content ?? "{}";

            // Try to parse JSON from the response
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var result = JsonSerializer.Deserialize<FoodAnalysisResult>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new FoodAnalysisResult();
            }

            _logger.LogWarning("Could not parse food analysis response: {Content}", content);
            return new FoodAnalysisResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing food image");
            return new FoodAnalysisResult();
        }
    }

    private Kernel CreateKernel(Guid userId)
    {
        var builder = Kernel.CreateBuilder();

        var apiKey = _configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI API key not configured");

        builder.AddOpenAIChatCompletion(
            modelId: _configuration["OpenAI:ModelId"] ?? "gpt-4o",
            apiKey: apiKey);

        var kernel = builder.Build();

        // Add the nutrition plugin
        var nutritionPlugin = new NutritionPlugin(_context, userId);
        kernel.ImportPluginFromObject(nutritionPlugin, "Nutrition");

        return kernel;
    }
}
