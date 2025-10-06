using RecipeApp.Data;
using RecipeApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace RecipeApp.Services
{
    public class MealPlanParser
    {
        private readonly AppDb _db;

        public MealPlanParser(AppDb db)
        {
            _db = db;
        }

        // ✅ Enhanced parser: returns recipeIds, freeTextItems, and unmatched meals
        public (List<Guid> recipeIds, List<string> freeTextItems, List<(string MealType, string MealName)> noMatches)
            ParseWithNoMatches(string text)
        {
            var recipeIds = new List<Guid>();
            var freeItems = new List<string>();
            var noMatches = new List<(string, string)>();

            if (string.IsNullOrWhiteSpace(text))
                return (recipeIds, freeItems, noMatches);

            text = Regex.Replace(text, @"\s+", " ").Trim();

            var allRecipes = _db.Recipes
                .Select(r => new { r.Id, Name = r.Title.ToLower() })
                .ToList();

            // Split by meal headings
            var sections = Regex.Split(text, @"(?i)\b(breakfast|lunch|dinner|snack|mid-morning|mid-afternoon)\b")
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            foreach (var section in sections)
            {
                bool matched = false;
                string mealType = "Meal";

                if (Regex.IsMatch(section, @"(?i)\bbreakfast\b")) mealType = "Breakfast";
                else if (Regex.IsMatch(section, @"(?i)\blunch\b")) mealType = "Lunch";
                else if (Regex.IsMatch(section, @"(?i)\bdinner\b")) mealType = "Dinner";
                else if (Regex.IsMatch(section, @"(?i)\bsnack\b")) mealType = "Snack";

                foreach (var recipe in allRecipes)
                {
                    if (section.ToLower().Contains(recipe.Name))
                    {
                        recipeIds.Add(recipe.Id);
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    // Treat as “no match”
                    var cleaned = Regex.Replace(section, @"[^\w\s]", "").Trim();
                    if (cleaned.Length > 3)
                        noMatches.Add((mealType, cleaned));
                }
            }

            return (recipeIds.Distinct().ToList(), freeItems.Distinct().ToList(), noMatches.Distinct().ToList());
        }
    }
}
