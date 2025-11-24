using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace RecipeApp.Services
{
    /// <summary>
    /// Uses OpenAI to estimate per-serving calories, protein, carbs and fats for meals/recipes.
    /// </summary>
    public class LlmNutritionEstimator
    {
        private readonly OpenAIClient _client;
        private readonly ILogger<LlmNutritionEstimator> _logger;
        private static readonly Dictionary<string, NutritionEstimate> _cache = new(StringComparer.OrdinalIgnoreCase);

        public LlmNutritionEstimator(OpenAIClient client, ILogger<LlmNutritionEstimator> logger)
        {
            _client = client;
            _logger = logger;
        }

        public Task<NutritionEstimate?> EstimateRecipeAsync(
            string name,
            IEnumerable<string>? ingredients,
            int? servings,
            CancellationToken cancellationToken = default) =>
            EstimateAsync(BuildRecipeDescription(name, ingredients, servings), cancellationToken);

        public Task<NutritionEstimate?> EstimateFreeTextAsync(
            string description,
            CancellationToken cancellationToken = default) =>
            EstimateAsync($"Meal description:\n{description}", cancellationToken);

        private async Task<NutritionEstimate?> EstimateAsync(string description, CancellationToken cancellationToken)
        {
            var cacheKey = description.Trim();
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                var systemPrompt =
                    "You are a sports nutritionist. " +
                    "Estimate per-serving calories (kcal), protein (grams), carbs (grams) and fat (grams) " +
                    "for the provided meal description. " +
                    "Always respond with strict JSON matching " +
                    "{ \"calories\": number, \"protein\": number, \"carbs\": number, \"fat\": number }. " +
                    "If servings are included, assume output is per serving. " +
                    "Use decimals when helpful. No commentary or explanation.";

                var messages = new List<ChatMessage>
                {
                    ChatMessage.CreateSystemMessage(systemPrompt),
                    ChatMessage.CreateUserMessage(description)
                };

                var chatClient = _client.GetChatClient("gpt-4.1-mini");
                var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
                var text = response.Value.Content[0].Text ?? string.Empty;
                text = text.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();

                var payload = JsonSerializer.Deserialize<NutritionEstimatePayload>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (payload == null)
                    return null;

                var estimate = payload.ToEstimate();
                _cache[cacheKey] = estimate;
                return estimate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM nutrition estimation failed.");
                return null;
            }
        }

        private static string BuildRecipeDescription(string name, IEnumerable<string>? ingredients, int? servings)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Recipe: {name}");
            if (servings.HasValue && servings.Value > 0)
            {
                sb.AppendLine($"Servings: {servings.Value}");
            }

            var list = ingredients?
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim())
                .ToList();

            if (list != null && list.Count > 0)
            {
                sb.AppendLine("Ingredients:");
                foreach (var ingredient in list)
                {
                    sb.AppendLine($"- {ingredient}");
                }
            }

            return sb.ToString();
        }

        private sealed class NutritionEstimatePayload
        {
            public decimal Calories { get; set; }
            public decimal Protein { get; set; }
            public decimal Carbs { get; set; }
            public decimal Fat { get; set; }

            public NutritionEstimate ToEstimate() => new()
            {
                Calories = Calories,
                Protein = Protein,
                Carbs = Carbs,
                Fat = Fat,
                Estimated = true
            };
        }
    }

    public sealed class NutritionEstimate
    {
        public decimal Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Carbs { get; set; }
        public decimal Fat { get; set; }
        public bool Estimated { get; set; }
    }
}
