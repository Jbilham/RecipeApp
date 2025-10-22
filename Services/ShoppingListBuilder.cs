using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;

namespace RecipeApp.Services;

/// <summary>
/// Builds a normalised, deduplicated, alphabetised shopping list
/// from recipe and free-text meal data.
/// </summary>
public class ShoppingListBuilder
{
    private readonly AppDb _db;
    public ShoppingListBuilder(AppDb db) => _db = db;

    /// <summary>
    /// Aggregates all recipe ingredients and free-text items into one list.
    /// </summary>
    public async Task<ShoppingListResponse> BuildAsync(
        IEnumerable<Guid> recipeIds,
        IEnumerable<string>? extraItems = null)
    {
        Console.WriteLine("🔹 Building shopping list…");

        var ids = recipeIds?.Distinct().ToList() ?? new();
        var rows = await _db.RecipeIngredients
            .Include(ri => ri.Ingredient)
            .Include(ri => ri.Unit)
            .Where(ri => ids.Contains(ri.RecipeId))
            .AsNoTracking()
            .ToListAsync();

        string Normalize(string input) =>
            input.ToLowerInvariant()
                 .Replace("&", "and")
                 .Replace("-", " ")
                 .Replace("x ", "")
                 .Replace("s,", ",")
                 .Replace("  ", " ")
                 .Trim();

        var grouped = new Dictionary<string, ShoppingListItemDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var ri in rows)
        {
            var key = Normalize(ri.Ingredient.Name);
            if (!grouped.TryGetValue(key, out var item))
            {
                item = new ShoppingListItemDto
                {
                    Ingredient = key,
                    Amount = 0,
                    Unit = ri.Unit?.Code,
                    SourceRecipes = new List<Guid>()
                };
                grouped[key] = item;
            }

            if (ri.Amount.HasValue)
                item.Amount = (item.Amount ?? 0) + ri.Amount.Value;

            if (!item.SourceRecipes.Contains(ri.RecipeId))
                item.SourceRecipes.Add(ri.RecipeId);
        }

        if (extraItems != null)
        {
            foreach (var extra in extraItems)
            {
                if (string.IsNullOrWhiteSpace(extra)) continue;
                var key = Normalize(extra);
                if (!grouped.ContainsKey(key))
                {
                    grouped[key] = new ShoppingListItemDto
                    {
                        Ingredient = key,
                        Amount = null,
                        Unit = null,
                        SourceRecipes = new List<Guid>()
                    };
                }
            }
        }

        foreach (var item in grouped.Values)
        {
            if (!string.IsNullOrWhiteSpace(item.Ingredient))
                item.Ingredient = char.ToUpper(item.Ingredient[0]) + item.Ingredient[1..];
        }

        var list = grouped.Values.OrderBy(i => i.Ingredient).ToList();
        Console.WriteLine($"✅ Shopping list built ({list.Count} items)");
        return new ShoppingListResponse { Items = list };
    }
}
