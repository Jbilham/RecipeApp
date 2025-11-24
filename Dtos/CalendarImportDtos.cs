using System;
using System.Collections.Generic;
using RecipeApp.Models;

namespace RecipeApp.Dtos
{
    public class CalendarImportMealDto
    {
        public Guid MealId { get; set; }
        public string MealType { get; set; } = string.Empty;
        public Guid? RecipeId { get; set; }
        public string? RecipeName { get; set; }
        public bool MissingRecipe { get; set; }
        public bool AutoHandled { get; set; }
        public string? FreeText { get; set; }
        public bool IsSelected { get; set; } = true;
        public MealNutritionDto? Nutrition { get; set; }
    }

    public class CalendarImportPlanDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public List<CalendarImportMealDto> Meals { get; set; } = new();
        public NutritionBreakdownDto? NutritionTotals { get; set; }
    }

    public class CalendarImportMissingMealDto
    {
        public Guid MealPlanId { get; set; }
        public string MealPlanName { get; set; } = string.Empty;
        public string MealType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class CalendarImportResult
    {
        public string Range { get; set; } = string.Empty;
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public int TotalPlans { get; set; }
        public Guid ShoppingListId { get; set; }
        public Guid MealPlanSnapshotId { get; set; }
        public List<CalendarImportPlanDto> Plans { get; set; } = new();
        public List<CalendarImportMissingMealDto> MissingMeals { get; set; } = new();
        public ShoppingListResponse ShoppingList { get; set; } = new();
        public MealPlanNutritionSummaryDto NutritionSummary { get; set; } = new();
    }
}
