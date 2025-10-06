
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShoppingListController : ControllerBase
    {
        private readonly AppDb _db;
        public ShoppingListController(AppDb db) => _db = db;

        [HttpPost]
        public async Task<ActionResult<ShoppingListResponse>> Build(ShoppingListRequest request)
        {
            var rows = await _db.RecipeIngredients
                .Include(ri => ri.Ingredient)
                .Include(ri => ri.Unit)
                .Where(ri => request.RecipeIds.Contains(ri.RecipeId))
                .ToListAsync();

            var grouped = new Dictionary<string, ShoppingListItemDto>(StringComparer.OrdinalIgnoreCase);

            // ✅ Handle recipe-derived ingredients
            foreach (var ri in rows)
            {
                var name = ri.Ingredient.NameCanonical;

                if (!grouped.TryGetValue(name, out var item))
                {
                    item = new ShoppingListItemDto
                    {
                        Ingredient = ri.Ingredient.NameDisplay,
                        Amount = 0,
                        Unit = ri.Unit?.Code,
                        SourceRecipes = new List<Guid>()
                    };
                    grouped[name] = item;
                }

                if (ri.Amount.HasValue)
                {
                    item.Amount = (item.Amount ?? 0) + ri.Amount.Value;
                }

                if (!item.SourceRecipes.Contains(ri.RecipeId))
                    item.SourceRecipes.Add(ri.RecipeId);
            }

            // ✅ Handle free-text items
            if (request.FreeItems != null)
            {
                foreach (var freeItem in request.FreeItems.Where(i => !string.IsNullOrWhiteSpace(i)))
                {
                    var name = freeItem.Trim();

                    if (!grouped.TryGetValue(name, out var item))
                    {
                        item = new ShoppingListItemDto
                        {
                            Ingredient = name,
                            Amount = null, // no structured amount
                            Unit = null,
                            SourceRecipes = new List<Guid>() // not tied to recipes
                        };
                        grouped[name] = item;
                    }
                }
            }

            return new ShoppingListResponse 
            { 
                Items = grouped.Values.OrderBy(i => i.Ingredient).ToList() 
            };
        }
    }
}
