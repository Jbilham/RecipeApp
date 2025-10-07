using System.Text.Json;
using OpenAI;
using OpenAI.Chat;

namespace RecipeApp.Services
{
    /// <summary>
    /// Uses OpenAI to intelligently normalize ingredient names (merge variants, remove noise, etc.).
    /// Includes simple in-memory caching to avoid duplicate API calls.
    /// </summary>
    public class LlmIngredientNormalizer
    {
        private readonly OpenAIClient _client;
        private static readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

        public LlmIngredientNormalizer(IConfiguration config)
        {
            var apiKey = config["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI:ApiKey missing in configuration.");
            _client = new OpenAIClient(apiKey);
        }

        public async Task<Dictionary<string, string>> NormalizeAsync(IEnumerable<string> ingredientNames)
        {
            var names = ingredientNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!names.Any()) return new();

            // Use cached results where possible
            var uncached = names.Where(x => !_cache.ContainsKey(x)).ToList();
            if (uncached.Any())
            {
                var newMappings = await CallOpenAIAsync(uncached);
                foreach (var kvp in newMappings)
                    _cache[kvp.Key] = kvp.Value;
            }

            // Build combined result
            return names.ToDictionary(x => x, x => _cache.TryGetValue(x, out var v) ? v : x);
        }

        private async Task<Dictionary<string, string>> CallOpenAIAsync(List<string> ingredients)
        {
            var prompt = @$"
You are a professional food and grocery data normalizer.

Given a list of ingredient names, return a JSON dictionary mapping each original name to a cleaned, standardized version.

Rules:
- Use singular forms (e.g. 'Tomatoes' → 'Tomato')
- Merge variants (e.g. 'Cherry Tomato', 'Tomatoe' → 'Tomato')
- Simplify brands ('Fage Total Greek Yoghurt' → 'Greek Yoghurt')
- Merge protein-related items ('Protein Yoghurt', 'Whey Protein Drink', 'Protein Bar') → 'Protein Snack'
- Remove non-food noise like 'Meal', 'Lunch', 'Dinner', 'Breakfast'
- Ignore measurements like 'g', 'ml', 'x', 'tbsp', 'tsp', etc.
- Return only clean, human-friendly ingredient names (e.g. 'Wholemeal Bread', 'Greek Yoghurt', 'Chicken Breast')
- Return ONLY valid JSON like:
{{ ""Tomatoes"": ""Tomato"", ""Protein Bar"": ""Protein Snack"" }}

Input:
{JsonSerializer.Serialize(ingredients, new JsonSerializerOptions { WriteIndented = true })}
";

            try
            {
                var chatClient = _client.GetChatClient("gpt-4o-mini");
                var messages = new List<ChatMessage>
                {
                    ChatMessage.CreateSystemMessage(prompt)
                };

                var response = await chatClient.CompleteChatAsync(messages);
                var text = response.Value.Content[0].Text?.Trim() ?? "{}";

                // Clean code block formatting if present
                text = text.Replace("```json", "").Replace("```", "").Trim();

                var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(text,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return mapping ?? ingredients.ToDictionary(x => x, x => x);
            }
            catch
            {
                // fallback
                return ingredients.ToDictionary(x => x, x => x);
            }
        }
    }
}
