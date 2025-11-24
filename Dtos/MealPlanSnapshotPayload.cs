using System;
using System.Collections.Generic;

namespace RecipeApp.Dtos
{
    public class MealPlanSnapshotPayload
    {
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public string Range { get; set; } = string.Empty;
        public List<MealPlanSnapshotPlan> Plans { get; set; } = new();
        public Guid? ShoppingListSnapshotId { get; set; }
        public NutritionBreakdownDto? WeeklyNutritionTotals { get; set; }
    }

    public class MealPlanSnapshotPlan
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public List<MealPlanSnapshotMeal> Meals { get; set; } = new();
        public NutritionBreakdownDto? NutritionTotals { get; set; }
    }

    public class MealPlanSnapshotMeal
    {
        public Guid MealId { get; set; }
        public string MealType { get; set; } = string.Empty;
        public string? RecipeName { get; set; }
        public bool MissingRecipe { get; set; }
        public bool AutoHandled { get; set; }
        public string? FreeText { get; set; }
        public bool IsSelected { get; set; } = true;
        public MealNutritionDto? Nutrition { get; set; }
    }
}
