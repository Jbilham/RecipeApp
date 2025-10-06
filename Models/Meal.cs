/* using System.ComponentModel.DataAnnotations;

namespace RecipeApp.Models
{
    public class Meal
    {
        [Key]
        public Guid Id { get; set; }

        // Optional link to a Recipe
        public Guid? RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        // Free text fallback (when no recipe is chosen)
        public string? FreeText { get; set; }

        // Breakfast, Lunch, Snack, etc
        public string MealType { get; set; } = string.Empty;

        // Parent MealPlan
        public Guid MealPlanId { get; set; }
        public MealPlan MealPlan { get; set; } = null!;
    }
}
*/
