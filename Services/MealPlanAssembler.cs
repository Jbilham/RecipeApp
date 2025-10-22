using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Models;
using RecipeApp.Dtos;
using System.Globalization;

namespace RecipeApp.Services;

/// <summary>
/// Shared logic for constructing weekly meal plans from parsed data.
/// </summary>
public class MealPlanAssembler
{
    private readonly AppDb _db;
    public MealPlanAssembler(AppDb db) => _db = db;

    public async Task<List<MealPlan>> BuildWeeklyPlansAsync(
        IEnumerable<LlmMealPlanParser.ParsedMealPlan> parsedPlans,
        DateTime weekStart)
    {
        Console.WriteLine("🔹 Assembling weekly meal plans…");

        var start = weekStart.Date;
        var end = start.AddDays(7);
        var results = new List<MealPlan>();

        foreach (var parsed in parsedPlans)
        {
            var date = parsed.Date ?? start;
            if (date < start || date >= end) continue;

            var dayName = date.ToString("dddd", CultureInfo.InvariantCulture);
            var meals = await MapParsedMealsAsync(parsed);

            var plan = new MealPlan
            {
                Id = Guid.NewGuid(),
                Name = $"Imported Plan - {dayName}",
                Date = date,
                Meals = meals
            };
            results.Add(plan);
        }

        Console.WriteLine($"✅ {results.Count} meal plans assembled");
        return results.OrderBy(r => r.Date).ToList();
    }

    private async Task<List<Meal>> MapParsedMealsAsync(LlmMealPlanParser.ParsedMealPlan parsed)
    {
        var meals = new List<Meal>();
        if (parsed?.Meals == null) return meals;

        string Normalize(string input) =>
            input.ToLowerInvariant().Replace("&", "and").Replace("-", " ").Replace("  ", " ").Trim();

        var allRecipes = await _db.Recipes.AsNoTracking()
            .Select(r => new { r.Id, r.Title })
            .ToListAsync();

        foreach (var pm in parsed.Meals)
        {
            string? normalizedName = pm.MatchedRecipeTitle ?? pm.UnmatchedMealTitle;
            Recipe? matchedRecipe = null;

            if (!string.IsNullOrWhiteSpace(normalizedName))
            {
                var norm = Normalize(normalizedName);
                matchedRecipe = await _db.Recipes
                    .FirstOrDefaultAsync(r => EF.Functions.ILike(r.Title, $"%{norm}%"))
                    ?? allRecipes.Select(r => new Recipe { Id = r.Id, Title = r.Title })
                        .FirstOrDefault(r =>
                            Normalize(r.Title).Contains(norm) ||
                            norm.Contains(Normalize(r.Title)));
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
