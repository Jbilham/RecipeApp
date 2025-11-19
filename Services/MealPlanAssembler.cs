using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Models;

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