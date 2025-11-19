using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;
using RecipeApp.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/shoppinglists")]
    public class ShoppingListController : ControllerBase
    {
        private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        private readonly AppDb _db;
        private readonly IUserContext _userContext;

        public ShoppingListController(AppDb db, IUserContext userContext)
        {
            _db = db;
            _userContext = userContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAllAsync()
        {
            var visibleUserIds = await _userContext.GetVisibleUserIdsAsync();

            var snapshotsQuery = _db.ShoppingListSnapshots
                .OrderByDescending(s => s.CreatedAt)
                .AsQueryable();

            snapshotsQuery = snapshotsQuery.Where(s =>
                s.CreatedById.HasValue && visibleUserIds.Contains(s.CreatedById.Value));

            var snapshots = await snapshotsQuery.ToListAsync();

            var list = snapshots.Select(snapshot =>
            {
                var payload = DeserializeSnapshot(snapshot.JsonData);
                var weekStart = payload?.WeekStart;
                var title = weekStart.HasValue
                    ? $"Week commencing Monday {weekStart.Value:dd MMM yyyy}"
                    : "Shopping List";

                return new
                {
                    id = snapshot.Id,
                    title,
                    weekStart,
                    weekEnd = payload?.WeekEnd,
                    createdAt = snapshot.CreatedAt,
                    range = payload?.Range,
                    itemCount = payload?.ShoppingList?.Items?.Count ?? 0,
                    mealPlanSnapshotId = payload?.MealPlanSnapshotId
                };
            });

            return Ok(list);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<object>> GetByIdAsync(Guid id)
        {
            var visibleUserIds = await _userContext.GetVisibleUserIdsAsync();

            var snapshot = await _db.ShoppingListSnapshots
                .FirstOrDefaultAsync(s => s.Id == id &&
                    s.CreatedById.HasValue &&
                    visibleUserIds.Contains(s.CreatedById.Value));
            if (snapshot == null)
                return NotFound();

            var payload = DeserializeSnapshot(snapshot.JsonData);
            if (payload == null)
                return BadRequest("Snapshot is corrupted.");

            var title = payload.WeekStart != default
                ? $"Week commencing Monday {payload.WeekStart:dd MMM yyyy}"
                : "Shopping List";

            return Ok(new
            {
                id = snapshot.Id,
                title,
                range = payload.Range,
                weekStart = payload.WeekStart,
                weekEnd = payload.WeekEnd,
                createdAt = snapshot.CreatedAt,
                items = payload.ShoppingList?.Items ?? new List<ShoppingListItemDto>(),
                shoppingList = payload.ShoppingList,
                plans = payload.Plans,
                mealPlanSnapshotId = payload.MealPlanSnapshotId
            });
        }

        [HttpPost("build")]
        public async Task<ActionResult<ShoppingListResponse>> BuildAsync(ShoppingListRequest request)
        {
            var visibleUserIds = await _userContext.GetVisibleUserIdsAsync();

            var rows = await _db.RecipeIngredients
                .Include(ri => ri.Ingredient)
                .Include(ri => ri.Unit)
                .Where(ri => request.RecipeIds.Contains(ri.RecipeId))
                .Join(_db.Recipes,
                    ingredient => ingredient.RecipeId,
                    recipe => recipe.Id,
                    (ingredient, recipe) => new { ingredient, recipe })
                .Where(join =>
                    join.recipe.IsGlobal ||
                    (join.recipe.OwnerId.HasValue && visibleUserIds.Contains(join.recipe.OwnerId.Value)))
                .Select(join => join.ingredient)
                .ToListAsync();

            var grouped = new Dictionary<string, ShoppingListItemDto>(StringComparer.OrdinalIgnoreCase);

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
                            Amount = null,
                            Unit = null,
                            SourceRecipes = new List<Guid>()
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

        private static ShoppingListSnapshotPayload? DeserializeSnapshot(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<ShoppingListSnapshotPayload>(json, SnapshotJsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }
}
