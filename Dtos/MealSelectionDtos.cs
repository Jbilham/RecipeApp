using System;
using System.Collections.Generic;

namespace RecipeApp.Dtos
{
    public class UpdateMealSelectionsDto
    {
        public List<MealSelectionDto> Meals { get; set; } = new();
    }

    public class MealSelectionDto
    {
        public Guid MealId { get; set; }
        public bool IsSelected { get; set; }
    }
}
