using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Chat;
using RecipeApp.Data;

namespace RecipeApp.Services
{
    public class LlmMealPlanParser
    {
        private readonly AppDb _db;
        private readonly OpenAIClient _oa;

        public LlmMealPlanParser(AppDb db, IConfiguration config)
        {
            _db = db;
            var apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey missing");
            _oa = new OpenAIClient(apiKey);
        }

        // -------------------- Helper Regex for quantities --------------------

        // 🔹 Regex to capture "100g chicken", "2 x eggs", "1 cup oats", etc.
        private static readonly Regex _quantityRegex =
            new(@"(?:(?<qty>\d+(?:\.\d+)?)\s*(?<unit>g|kg|ml|l|tbsp|tsp|cup|cups|slice|slices|x)?\s*)?(?<name>[A-Za-z][A-Za-z\s\-]+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Extracts (name, quantity, unit) from a single free-text ingredient line.
        /// Returns nulls when no numeric data present.
        /// </summary>
        public static (string name, float? qty, string? unit) ParseIngredientText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return (string.Empty, null, null);

            var m = _quantityRegex.Match(raw.Trim());
            if (!m.Success) return (raw.Trim(), null, null);

            float? qty = null;
            if (float.TryParse(m.Groups["qty"].Value, out var q))
                qty = q;

            var unit = m.Groups["unit"].Value;
            var name = m.Groups["name"].Value.Trim();

            return (name, qty, string.IsNullOrWhiteSpace(unit) ? null : unit);
        }

        // -------------------- Main LLM Parsing Logic --------------------

        public async Task<ParsedMealPlan> ParseAsync(string freeText, CancellationToken ct = default)
        {
            var result = new ParsedMealPlan();

            if (string.IsNullOrWhiteSpace(freeText))
                return result;

            // Load all recipe names
            var dbRecipes = await _db.Recipes
                .AsNoTracking()
                .Select(r => r.Title)
                .ToListAsync(ct);

            var knownTitles = string.Join("\n", dbRecipes.Select(t => $"- {t}"));

            var chatClient = _oa.GetChatClient("gpt-4.1-mini");

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(
                    "You are a strict meal-plan parser. You MUST return only valid JSON with this exact schema:\n" +
                    "{ \"meals\": [ { \"mealType\": string, \"matchedRecipeTitle\": string|null, \"unmatchedMealTitle\": string|null, \"freeTextItems\": string[] } ] }\n" +
                    "Rules:\n" +
                    "1) mealType should be one of: Breakfast, Lunch, Dinner, Snack, Mid-morning, Mid-afternoon (best guess).\n" +
                    "2) matchedRecipeTitle must be the exact text of a title from the KNOWN_RECIPES list if you find a clear match; otherwise null.\n" +
                    "3) If no match found but it looks like a named dish, set unmatchedMealTitle.\n" +
                    "4) Extract extra 'freeTextItems' mentioned in that meal (e.g., '1 portion of fruit', 'whey protein shake').\n" +
                    "5) No markdown, no commentary, return ONLY JSON."
                ),
                ChatMessage.CreateUserMessage($"KNOWN_RECIPES:\n{knownTitles}\n\nMEAL_PLAN_TEXT:\n{freeText}")
            };

            var resp = await chatClient.CompleteChatAsync(messages);

            var json = resp.Value.Content[0].Text ?? "";
            json = Regex.Replace(json, @"^```json\s*|\s*```$", "", RegexOptions.Multiline);

            var parsed = JsonSerializer.Deserialize<ParsedMealPlan>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (parsed?.Meals != null)
            {
                foreach (var meal in parsed.Meals)
                {
                    foreach (var item in meal.FreeTextItems)
                        meal.ParsedFreeItemsDetailed.Add(ParseIngredientText(item));
                }
            }

            return parsed ?? result;
        }

        // -------------------- Data Structures --------------------

        public class ParsedMealPlan
        {
            public List<ParsedMeal> Meals { get; set; } = new();
        }

        public class ParsedMeal
        {
            public string MealType { get; set; } = "Meal";
            public string? MatchedRecipeTitle { get; set; }
            public string? UnmatchedMealTitle { get; set; }
            public List<string> FreeTextItems { get; set; } = new();

            // 🆕 Structured version of FreeTextItems
            public List<(string name, float? qty, string? unit)> ParsedFreeItemsDetailed { get; set; } = new();
        }
    }
}
