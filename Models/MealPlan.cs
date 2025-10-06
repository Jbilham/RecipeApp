using System;
using System.Collections.Generic;

namespace RecipeApp.Models
{
    public class MealPlan
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;  // e.g. "Week 1 Day 1"
        public DateTime? Date { get; set; }

        public ICollection<Meal> Meals { get; set; } = new List<Meal>();
    }

    public class Meal
    {
        public Guid Id { get; set; }
        public Guid MealPlanId { get; set; }
        public MealPlan MealPlan { get; set; } = null!;

        public string MealType { get; set; } = "";  // e.g. Breakfast, Lunch
        public Guid? RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        public string? FreeText { get; set; } // "Protein shake + banana"
    }
}
