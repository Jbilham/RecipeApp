using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;

namespace RecipeApp.Services
{
    public class MealPlanNutritionService
    {
        private readonly AppDb _db;
        private readonly LlmNutritionEstimator _estimator;

        public MealPlanNutritionService(AppDb db, LlmNutritionEstimator estimator)
        {
            _db = db;
            _estimator = estimator;
        }

        public async Task<NutritionComputationResult> EnsureNutritionAsync(
            IEnumerable<MealPlan> plans,
            CancellationToken cancellationToken = default)
        {
            var planList = plans?.Where(p => p != null).Distinct().ToList() ?? new List<MealPlan>();
            if (planList.Count == 0)
                return NutritionComputationResult.Empty;

            await PopulateMissingNutritionAsync(planList, cancellationToken);
            return BuildSummary(planList);
        }

        public NutritionComputationResult BuildSummary(IEnumerable<MealPlan> plans)
        {
            var planList = plans?.Where(p => p != null).ToList() ?? new List<MealPlan>();
            if (planList.Count == 0)
                return NutritionComputationResult.Empty;

            var result = new NutritionComputationResult();
            foreach (var plan in planList)
            {
                var totals = SumMeals(plan.Meals);
                if (totals.HasValues)
                {
                    result.PlanTotals[plan.Id] = totals;
                    AddInto(result.WeeklyTotals, totals);
                }
            }

            return result;
        }

        private async Task PopulateMissingNutritionAsync(List<MealPlan> plans, CancellationToken cancellationToken)
        {
            var meals = plans.SelectMany(p => p.Meals).ToList();
            if (meals.Count == 0)
                return;

            var recipeIds = meals
                .Where(m => m.RecipeId.HasValue)
                .Select(m => m.RecipeId!.Value)
                .Distinct()
                .ToList();

            var recipes = recipeIds.Count == 0
                ? new Dictionary<Guid, Recipe>()
                : await _db.Recipes
                    .Include(r => r.RecipeIngredients)
                        .ThenInclude(ri => ri.Ingredient)
                    .Include(r => r.RecipeIngredients)
                        .ThenInclude(ri => ri.Unit)
                    .Where(r => recipeIds.Contains(r.Id))
                    .ToDictionaryAsync(r => r.Id, cancellationToken);

            var descriptionCache = new Dictionary<string, NutritionEstimate>(StringComparer.OrdinalIgnoreCase);

            foreach (var meal in meals)
            {
                if (HasNutrition(meal))
                    continue;

                NutritionEstimate? estimate;
                string source;
                bool estimatedFlag;

                if (meal.RecipeId.HasValue && recipes.TryGetValue(meal.RecipeId.Value, out var recipe))
                {
                    estimate = await EnsureRecipeNutritionAsync(recipe, cancellationToken);
                    if (estimate == null)
                        continue;

                    source = recipe.Title;
                    estimatedFlag = recipe.MacrosEstimated || estimate.Estimated;
                }
                else
                {
                    var descriptor = BuildFreeTextDescriptor(meal);
                    if (string.IsNullOrWhiteSpace(descriptor))
                        continue;

                    if (!descriptionCache.TryGetValue(descriptor, out estimate))
                    {
                        estimate = await _estimator.EstimateFreeTextAsync(descriptor, cancellationToken);
                        if (estimate != null)
                        {
                            descriptionCache[descriptor] = estimate;
                        }
                    }

                    if (estimate == null)
                        continue;

                    source = BuildSourceLabel(meal);
                    estimatedFlag = true;
                }

                ApplyToMeal(meal, estimate, source, estimatedFlag);
            }
        }

        private async Task<NutritionEstimate?> EnsureRecipeNutritionAsync(Recipe recipe, CancellationToken cancellationToken)
        {
            if (HasNutrition(recipe))
            {
                return new NutritionEstimate
                {
                    Calories = recipe.Calories ?? 0,
                    Protein = recipe.Protein ?? 0,
                    Carbs = recipe.Carbs ?? 0,
                    Fat = recipe.Fat ?? 0,
                    Estimated = recipe.MacrosEstimated
                };
            }

            var ingredients = recipe.RecipeIngredients
                .Select(ri =>
                {
                    var amount = ri.Amount.HasValue
                        ? ri.Amount.Value.ToString("0.##", CultureInfo.InvariantCulture)
                        : null;
                    var unit = ri.Unit?.Code;
                    var pieces = new[] { amount, unit, ri.Ingredient.Name }
                        .Where(part => !string.IsNullOrWhiteSpace(part));
                    return string.Join(" ", pieces);
                })
                .Where(line => !string.IsNullOrWhiteSpace(line));

            var estimate = await _estimator.EstimateRecipeAsync(recipe.Title, ingredients, recipe.Servings, cancellationToken);
            if (estimate != null)
            {
                recipe.Calories = estimate.Calories;
                recipe.Protein = estimate.Protein;
                recipe.Carbs = estimate.Carbs;
                recipe.Fat = estimate.Fat;
                recipe.MacrosEstimated = true;
            }

            return estimate;
        }

        private static string BuildFreeTextDescriptor(Meal meal)
        {
            var components = new List<string>();
            if (!string.IsNullOrWhiteSpace(meal.MealType))
                components.Add($"Meal type: {meal.MealType}");

            if (!string.IsNullOrWhiteSpace(meal.FreeText))
                components.Add($"Description: {meal.FreeText}");

            if (meal.ExtraItems?.Count > 0)
                components.Add("Sides/snacks: " + string.Join(", ", meal.ExtraItems));

            return string.Join("\n", components);
        }

        private static string BuildSourceLabel(Meal meal)
        {
            if (meal.Recipe != null)
                return meal.Recipe.Title;
            if (meal.RecipeId.HasValue)
                return "Recipe";
            if (!string.IsNullOrWhiteSpace(meal.FreeText))
                return meal.FreeText;
            return meal.MealType ?? "Meal";
        }

        private static void ApplyToMeal(Meal meal, NutritionEstimate estimate, string source, bool estimated)
        {
            meal.Calories = estimate.Calories;
            meal.Protein = estimate.Protein;
            meal.Carbs = estimate.Carbs;
            meal.Fat = estimate.Fat;
            meal.NutritionSource = source;
            meal.NutritionEstimated = estimated;
        }

        private static NutritionBreakdownDto SumMeals(IEnumerable<Meal> meals)
        {
            var totals = new NutritionBreakdownDto();
            if (meals == null)
                return totals;

            foreach (var meal in meals.Where(m => m.IsSelected))
            {
                totals.Calories += meal.Calories ?? 0;
                totals.Protein += meal.Protein ?? 0;
                totals.Carbs += meal.Carbs ?? 0;
                totals.Fat += meal.Fat ?? 0;
            }
            return totals;
        }

        private static bool HasNutrition(Recipe recipe) =>
            recipe.Calories.HasValue &&
            recipe.Protein.HasValue &&
            recipe.Carbs.HasValue &&
            recipe.Fat.HasValue;

        private static bool HasNutrition(Meal meal) =>
            meal.Calories.HasValue &&
            meal.Protein.HasValue &&
            meal.Carbs.HasValue &&
            meal.Fat.HasValue;

        private static void AddInto(NutritionBreakdownDto target, NutritionBreakdownDto addition)
        {
            target.Calories += addition.Calories;
            target.Protein += addition.Protein;
            target.Carbs += addition.Carbs;
            target.Fat += addition.Fat;
        }
    }

    public class NutritionComputationResult
    {
        public static NutritionComputationResult Empty => new()
        {
            PlanTotals = new Dictionary<Guid, NutritionBreakdownDto>(),
            WeeklyTotals = new NutritionBreakdownDto()
        };

        public Dictionary<Guid, NutritionBreakdownDto> PlanTotals { get; set; } = new();
        public NutritionBreakdownDto WeeklyTotals { get; set; } = new();
    }
}
