using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;
using RecipeApp.Services;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MealPlanController : ControllerBase
    {
        private readonly AppDb _db;
        private readonly LlmMealPlanParser _llm;
        private readonly LlmIngredientNormalizer _normalizer;

        public MealPlanController(AppDb db, LlmMealPlanParser llm, LlmIngredientNormalizer normalizer)
        {
            _db = db;
            _llm = llm;
            _normalizer = normalizer;
        }

        [HttpPost("week")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<object>> CreateWeeklyMealPlan([FromForm] CreateWeeklyMealPlanDto dto)
        {
            if (dto is null) return BadRequest("Invalid weekly plan.");

            var weekText = new (string Day, string? Text)[]
            {
                ("Monday", dto.Monday),
                ("Tuesday", dto.Tuesday),
                ("Wednesday", dto.Wednesday),
                ("Thursday", dto.Thursday),
                ("Friday", dto.Friday),
                ("Saturday", dto.Saturday),
                ("Sunday", dto.Sunday)
            };

            var createdPlans = new List<MealPlan>();
            var allRecipeIds = new List<Guid>();
            var allFreeExtras = new List<string>();
            int offset = 0;

            foreach (var (day, text) in weekText)
            {
                if (string.IsNullOrWhiteSpace(text)) { offset++; continue; }

                var parsed = await _llm.ParseAsync(text ?? "");
                var meals = await MapParsedMealsAsync(parsed ?? new LlmMealPlanParser.ParsedMealPlan());
                allRecipeIds.AddRange(meals.Where(m => m.RecipeId.HasValue).Select(m => m.RecipeId!.Value));
                allFreeExtras.AddRange(ExtractFreeItemsFromMeals(meals));

                var plan = new MealPlan
                {
                    Id = Guid.NewGuid(),
                    Name = $"{dto.Name} - {day}",
                    Date = dto.StartDate.AddDays(offset),
                    Meals = SortMeals(meals)
                };

                _db.MealPlans.Add(plan);
                createdPlans.Add(plan);
                offset++;
            }

            await _db.SaveChangesAsync();

            // ✅ Reload plans from DB with related meals and recipes
            var planIds = createdPlans.Select(p => p.Id).ToList();
            var refreshedPlans = await _db.MealPlans
                .Where(p => planIds.Contains(p.Id))
                .Include(p => p.Meals)
                    .ThenInclude(m => m.Recipe)
                .AsNoTracking()
                .ToListAsync();

            // ✅ Sort by day
            string[] dayOrder = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            var orderedPlans = refreshedPlans.OrderBy(p =>
                Array.IndexOf(dayOrder,
                    dayOrder.FirstOrDefault(d => p.Name.Contains(d, StringComparison.OrdinalIgnoreCase)) ?? "")
            ).ToList();

            // ✅ Build aggregated shopping list
            var weeklyList = await BuildShoppingListAsync(allRecipeIds, allFreeExtras);

            // ✅ Save snapshot
            var snapshot = new ShoppingListSnapshot
            {
                Id = Guid.NewGuid(),
                JsonData = System.Text.Json.JsonSerializer.Serialize(weeklyList),
                SourceType = "weekly",
                CreatedAt = DateTime.UtcNow
            };
            _db.ShoppingListSnapshots.Add(snapshot);
            await _db.SaveChangesAsync();

            // ✅ Structured response
            var response = new
            {
                Plans = orderedPlans.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Date,
                    Meals = SortMeals(p.Meals).Select(m => new
                    {
                        m.Id,
                        m.MealType,
                        m.RecipeId,
                        RecipeName = m.RecipeId.HasValue
                            ? (m.Recipe?.Title ?? "(Recipe missing from DB)")
                            : $"{m.FreeText ?? "(No description)"} (No recipe available)",
                        m.FreeText
                    }),
                    p.FreeItems
                }),
                ShoppingList = weeklyList,
                SnapshotId = snapshot.Id
            };

            return Ok(response);
        }

        // ---------- Helpers ----------

        private static List<Meal> SortMeals(IEnumerable<Meal> meals)
        {
            string[] order = { "breakfast", "mid-morning", "morning", "lunch", "mid-afternoon", "afternoon", "dinner", "evening" };
            return meals
                .OrderBy(m =>
                {
                    var idx = Array.FindIndex(order, o =>
                        m.MealType != null && m.MealType.Contains(o, StringComparison.OrdinalIgnoreCase));
                    return idx >= 0 ? idx : int.MaxValue;
                })
                .ToList();
        }

        private static readonly string[] ProteinKeywords = {
            "protein", "whey", "shake", "bar", "yoghurt", "yogurt", "high protein"
        };

        private static readonly string[] FruitKeywords = {
            "fruit", "apple", "banana", "pear", "orange", "berries", "strawberry", "blueberry"
        };

        private static List<string> ExtractFreeItemsFromMeals(List<Meal> meals)
        {
            var extras = new List<string>();

            foreach (var m in meals)
            {
                if (string.IsNullOrWhiteSpace(m.FreeText)) continue;

                var parts = m.FreeText
                    .Split(new[] { '\n', ',', ';', '+', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();

                bool addedProtein = false;
                bool addedFruit = false;

                foreach (var part in parts)
                {
                    var lower = part.ToLowerInvariant();

                    if (ProteinKeywords.Any(k => lower.Contains(k)))
                    {
                        extras.Add("Protein snack");
                        addedProtein = true;
                        continue;
                    }

                    if (FruitKeywords.Any(k => lower.Contains(k)))
                    {
                        extras.Add("A piece of fruit");
                        addedFruit = true;
                        continue;
                    }

                    extras.Add(part);
                }
            }

            // ❌ No Distinct() here — we need to preserve duplicates for counting
            return extras;
        }

        private static string NormalizeIngredientName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var cleaned = input.ToLowerInvariant().Trim();

            cleaned = cleaned
                .Replace("arla", "")
                .Replace("brand", "")
                .Replace("example", "")
                .Replace("portion", "")
                .Replace("serving", "")
                .Replace("optional", "")
                .Replace(",", "")
                .Replace(".", "")
                .Replace("minced", "")
                .Replace("chopped", "")
                .Replace("peeled", "")
                .Replace("sliced", "")
                .Replace("grated", "")
                .Replace("deseeded", "")
                .Replace("finely", "")
                .Replace("large", "")
                .Replace("small", "")
                .Replace("fresh", "")
                .Replace("of", "")
                .Replace("the", "")
                .Replace("clove", "cloves")
                .Trim();

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned);
        }

        private async Task<ShoppingListResponse> BuildShoppingListAsync(List<Guid> recipeIds, List<string> extraItems)
        {
            var rows = await _db.RecipeIngredients
                .Include(ri => ri.Ingredient)
                .Include(ri => ri.Unit)
                .Where(ri => recipeIds.Contains(ri.RecipeId))
                .AsNoTracking()
                .ToListAsync();

            var grouped = new Dictionary<string, ShoppingListItemDto>(StringComparer.OrdinalIgnoreCase);

            void AddOrUpdate(string key, decimal? amount, string? unit, Guid? recipeId)
            {
                if (!grouped.TryGetValue(key, out var item))
                {
                    item = new ShoppingListItemDto
                    {
                        Ingredient = key,
                        Amount = 0,
                        Unit = unit,
                        SourceRecipes = new List<Guid>()
                    };
                    grouped[key] = item;
                }

                if (amount.HasValue)
                    item.Amount = (item.Amount ?? 0) + amount.Value;
                else
                    item.Amount = (item.Amount ?? 0) + 1; // count frequency

                if (recipeId.HasValue && !item.SourceRecipes.Contains(recipeId.Value))
                    item.SourceRecipes.Add(recipeId.Value);
            }

            foreach (var ri in rows)
                AddOrUpdate(NormalizeIngredientName(ri.Ingredient.Name), ri.Amount, ri.Unit?.Code, ri.RecipeId);

            foreach (var extra in extraItems)
            {
                var normalized = NormalizeIngredientName(extra);
                if (string.IsNullOrWhiteSpace(normalized)) continue;

                if (FruitKeywords.Any(k => normalized.ToLowerInvariant().Contains(k)))
                {
                    AddOrUpdate("Fruit (1 portion)", null, "pcs", null);
                    continue;
                }

                if (ProteinKeywords.Any(k => normalized.ToLowerInvariant().Contains(k)))
                {
                    AddOrUpdate("Protein (e.g. shake, bar, yoghurt)", null, "serving", null);
                    continue;
                }

                AddOrUpdate(normalized, null, null, null);
            }

            return new ShoppingListResponse { Items = grouped.Values.ToList() };
        }

        private async Task<List<Meal>> MapParsedMealsAsync(LlmMealPlanParser.ParsedMealPlan parsed)
        {
            var meals = new List<Meal>();
            if (parsed?.Meals is null) return meals;

            string Normalize(string input) =>
                input.ToLower().Replace("&", "and").Replace("-", " ").Replace("  ", " ").Trim();

            var allRecipes = await _db.Recipes.AsNoTracking().Select(r => new { r.Id, r.Title }).ToListAsync();

            foreach (var pm in parsed.Meals)
            {
                string? normalizedName = pm.MatchedRecipeTitle ?? pm.UnmatchedMealTitle;
                Recipe? matchedRecipe = null;

                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    var normalizedSearch = Normalize(normalizedName);
                    matchedRecipe = await _db.Recipes
                        .FirstOrDefaultAsync(r => EF.Functions.ILike(r.Title, $"%{normalizedSearch}%"))
                        ?? allRecipes
                            .Select(r => new Recipe { Id = r.Id, Title = r.Title })
                            .FirstOrDefault(r =>
                                Normalize(r.Title).Contains(normalizedSearch) ||
                                normalizedSearch.Contains(Normalize(r.Title)));
                }

                string? combinedFreeText = pm.FreeTextItems != null && pm.FreeTextItems.Any()
                    ? string.Join(", ", pm.FreeTextItems)
                    : null;

                meals.Add(new Meal
                {
                    Id = Guid.NewGuid(),
                    MealType = pm.MealType ?? "Meal",
                    RecipeId = matchedRecipe?.Id,
                    FreeText = matchedRecipe == null
                        ? normalizedName ?? combinedFreeText ?? "Meal"
                        : combinedFreeText
                });
            }

            return meals;
        }
    }
}
