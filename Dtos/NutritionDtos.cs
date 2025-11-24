using System;
using System.Collections.Generic;

namespace RecipeApp.Dtos
{
    public class NutritionBreakdownDto
    {
        public decimal Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Carbs { get; set; }
        public decimal Fat { get; set; }

        public bool HasValues =>
            Calories > 0 ||
            Protein > 0 ||
            Carbs > 0 ||
            Fat > 0;
    }

    public class MealNutritionDto : NutritionBreakdownDto
    {
        public string? Source { get; set; }
        public bool Estimated { get; set; }
    }

    public class PlanNutritionSummaryDto
    {
        public Guid MealPlanId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public NutritionBreakdownDto Totals { get; set; } = new();
    }

    public class MealPlanNutritionSummaryDto
    {
        public NutritionBreakdownDto WeeklyTotals { get; set; } = new();
        public List<PlanNutritionSummaryDto> Plans { get; set; } = new();
    }
}
