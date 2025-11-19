using System.Linq;
using RecipeApp.Models;

namespace RecipeApp.Helpers
{
    public static class MealUtilities
    {
        public static bool ShouldAutoHandleMeal(Meal meal)
        {
            if (meal == null)
                return false;

            if (meal.RecipeId.HasValue)
            {
                return false;
            }

            var type = meal.MealType?.ToLowerInvariant() ?? string.Empty;
            if (type.Contains("mid-morning") ||
                type.Contains("mid morning") ||
                type.Contains("mid-afternoon") ||
                type.Contains("mid afternoon") ||
                type.Contains("snack"))
            {
                return true;
            }

            var text = meal.FreeText?.ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string[] keywords =
            {
                "protein yoghurt",
                "protein yogurt",
                "protein shake",
                "protein bar",
                "portion of fruit",
                "whey protein",
                "fruit (e.g.",
                "fruit (eg",
                "fruit (for example",
                "slice wholemeal toast",
                "toast",
                "omelette",
                "omelet",
                "salad",
                "halloumi",
                "tuna",
                "chicken",
                "eggs",
                "wrap"
            };

            return keywords.Any(k => text.Contains(k));
        }
    }
}
