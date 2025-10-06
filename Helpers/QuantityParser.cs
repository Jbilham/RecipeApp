using RecipeApp.Data;
using RecipeApp.Models;

namespace RecipeApp.Helpers
{
    public static class QuantityParser
    {
        public static decimal? ParseAmount(string? quantity)
        {
            if (string.IsNullOrWhiteSpace(quantity)) return null;

            var number = new string(quantity
                .TakeWhile(c => char.IsDigit(c) || c == '.' || c == ',')
                .ToArray());

            number = number.Replace(',', '.');
            return decimal.TryParse(number, out var result) ? result : (decimal?)null;
        }

        public static Guid? TryResolveUnitId(AppDb db, string? quantity)
        {
            if (string.IsNullOrWhiteSpace(quantity)) return null;

            var trimmed = quantity.Trim().ToLowerInvariant();

            string? unitCode = null;
            foreach (var code in new[] { "kg", "g", "ml", "l", "tbsp", "tsp", "cup", "item", "cups", "items" })
            {
                if (trimmed.EndsWith(" " + code) || trimmed.EndsWith(code))
                {
                    unitCode = code switch
                    {
                        "cups" => "cup",
                        "items" => "item",
                        _ => code
                    };
                    break;
                }
            }

            if (unitCode == null) return null;

            var unit = db.Units.FirstOrDefault(u => u.Code.ToLower() == unitCode);
            return unit?.Id;
        }
    }
}
