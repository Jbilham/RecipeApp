using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;

namespace RecipeApp.Services
{
    public class LlmMealPlanParser
    {
        private readonly AppDb _db;
        private readonly string _apiKey;
        private readonly IHttpClientFactory _httpClientFactory;

        public LlmMealPlanParser(AppDb db, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _apiKey = (config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey missing")).Trim();
        }

        // -------------------- Helper Regex for quantities --------------------
        private static readonly Regex _quantityRegex =
            new(@"(?:(?<qty>\d+(?:\.\d+)?)\s*(?<unit>g|kg|ml|l|tbsp|tsp|cup|cups|slice|slices|x)?\s*)?(?<name>[A-Za-z][A-Za-z\s\-]+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        public async Task<ParsedMealPlan?> ParseAsync(string freeText, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(freeText))
                return null;

            Console.WriteLine("🧠 Sending meal plan text to LLM for parsing…");

            var dbRecipes = await _db.Recipes
                .AsNoTracking()
                .Select(r => r.Title)
                .Where(t => t != null && t.Trim() != string.Empty)
                .Take(30) // keep prompt small to avoid request size errors
                .ToListAsync(ct);

            var knownTitles = string.Join("\n", dbRecipes.Select(t => $"- {t}"));
            var client = _httpClientFactory.CreateClient();

            var messages = new[]
            {
                new
                {
                    role = "system",
                    content =
                    "You are a meal-plan parser. Return ONLY valid JSON matching this schema:\n" +
                    "{ \"meals\": [ { \"mealType\": string, \"matchedRecipeTitle\": string|null, \"unmatchedMealTitle\": string|null, \"freeTextItems\": string[] } ] }\n" +
                    "Rules:\n" +
                    "1) mealType = Breakfast, Lunch, Dinner, Snack, Mid-morning, Mid-afternoon (best guess).\n" +
                    "2) matchedRecipeTitle = exact recipe name from KNOWN_RECIPES if clearly present.\n" +
                    "3) unmatchedMealTitle = fallback dish name if not found in known recipes.\n" +
                    "4) Extract ALL freeTextItems (snacks, sides, fruit, protein, etc.).\n" +
                    "5) NO commentary, only JSON."
                },
                new
                {
                    role = "user",
                    content = $"KNOWN_RECIPES:\n{knownTitles}\n\nMEAL_PLAN_TEXT:\n{freeText}"
                }
            };

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                var payload = new
                {
                    model = "gpt-4o-mini",
                    messages
                };
                req.Content = JsonContent.Create(payload);

                var resp = await client.SendAsync(req, ct);
                resp.EnsureSuccessStatusCode();

                using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                var json = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;

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

                Console.WriteLine("✅ LLM parse successful.");
                return parsed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ LLM parse failed: {ex}");
                return null;
            }
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
            public List<(string name, float? qty, string? unit)> ParsedFreeItemsDetailed { get; set; } = new();
        }
    }
}
