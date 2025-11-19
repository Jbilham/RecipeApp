using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Models;
using System.Globalization;
using System.Linq;

namespace RecipeApp.Services
{
    public class MealPlanAssembler
    {
        private readonly AppDb _db;
        private readonly LlmIngredientNormalizer _normalizer;

        public MealPlanAssembler(AppDb db, LlmIngredientNormalizer normalizer)
        {
            _db = db;
            _normalizer = normalizer;
        }

        public async Task<MealPlan> CreateMealPlanFromParsedAsync(
            LlmMealPlanParser.ParsedMealPlan parsed,
            DateTime date,
            string name)
        {
            var meals = await MapParsedMealsAsync(parsed);
            var plan = new MealPlan
            {
                Id = Guid.NewGuid(),
                Name = name,
                Date = DateTime.SpecifyKind(date, DateTimeKind.Unspecified),
                Meals = SortMeals(meals)
            };
            plan.FreeItems = plan.Meals.SelectMany(m => m.ExtraItems).ToList();
            _db.MealPlans.Add(plan);
            return plan;
        }

        // ---------- Mapping Logic ----------
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
                Guid? matchedRecipeId = null;

                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    var normalizedSearch = Normalize(normalizedName);
                    var candidate = await _db.Recipes
                        .AsNoTracking()
                        .Where(r => EF.Functions.ILike(r.Title, $"%{normalizedSearch}%"))
                        .Select(r => new { r.Id, r.Title })
                        .FirstOrDefaultAsync();

                    candidate ??= allRecipes.FirstOrDefault(r =>
                        Normalize(r.Title).Contains(normalizedSearch) ||
                        normalizedSearch.Contains(Normalize(r.Title)));

                    matchedRecipeId = candidate?.Id;
                }

                string? combinedFreeText = pm.FreeTextItems != null && pm.FreeTextItems.Any()
                    ? string.Join(", ", pm.FreeTextItems)
                    : null;

                var extraItems = ExtractExtraItems(pm);

                meals.Add(new Meal
                {
                    Id = Guid.NewGuid(),
                    MealType = pm.MealType ?? "Meal",
                    RecipeId = matchedRecipeId,
                    FreeText = matchedRecipeId == null
                        ? normalizedName ?? combinedFreeText ?? "Meal"
                        : combinedFreeText,
                    ExtraItems = extraItems,
                    IsSelected = true
                });
            }

            return meals;
        }

        private static List<string> ExtractExtraItems(LlmMealPlanParser.ParsedMeal pm)
        {
            var extras = new List<string>();

            if (pm?.ParsedFreeItemsDetailed?.Count > 0)
            {
                foreach (var detail in pm.ParsedFreeItemsDetailed)
                {
                    if (string.IsNullOrWhiteSpace(detail.name))
                        continue;

                    var name = detail.name.Trim();
                    string formatted;

                    if (detail.qty.HasValue)
                    {
                        var qty = detail.qty.Value;
                        var qtyString = Math.Abs(qty % 1) < 0.0001
                            ? ((int)Math.Round(qty)).ToString(CultureInfo.InvariantCulture)
                            : qty.ToString("0.##", CultureInfo.InvariantCulture);

                        formatted = string.IsNullOrWhiteSpace(detail.unit)
                            ? $"{qtyString} {name}"
                            : $"{qtyString} {detail.unit} {name}";
                    }
                    else if (!string.IsNullOrWhiteSpace(detail.unit))
                    {
                        formatted = $"{detail.unit} {name}";
                    }
                    else
                    {
                        formatted = name;
                    }

                    extras.Add(formatted.Trim());
                }
            }
            else if (pm?.FreeTextItems != null)
            {
                extras.AddRange(pm.FreeTextItems.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()));
            }

            if (pm != null && string.IsNullOrWhiteSpace(pm.MatchedRecipeTitle) && !string.IsNullOrWhiteSpace(pm.UnmatchedMealTitle))
            {
                extras.Add(pm.UnmatchedMealTitle.Trim());
            }

            return extras;
        }

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
    }
}
