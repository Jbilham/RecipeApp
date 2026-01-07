using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Models;
using RecipeApp.Dtos;
using RecipeApp.Services;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecipesController : ControllerBase
    {
        private readonly AppDb _db;
        private readonly IUserContext _userContext;

        public RecipesController(AppDb db, IUserContext userContext)
        {
            _db = db;
            _userContext = userContext;
        }

        /// <summary>
        /// Filter recipes by macros (only for Master/Nutritionist).
        /// </summary>
        [HttpGet("filter")]
        public async Task<IActionResult> Filter(
            [FromQuery] decimal? minCalories = null,
            [FromQuery] decimal? maxCalories = null,
            [FromQuery] decimal? minProtein = null,
            [FromQuery] decimal? maxProtein = null,
            [FromQuery] decimal? minCarbs = null,
            [FromQuery] decimal? maxCarbs = null,
            [FromQuery] decimal? minFat = null,
            [FromQuery] decimal? maxFat = null)
        {
            var currentUser = await _userContext.GetCurrentUserAsync();
            if (!string.Equals(currentUser.Role, "Master", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(currentUser.Role, "Nutritionist", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var visibleUserIds = await _userContext.GetVisibleUserIdsAsync();

            var query = _db.Recipes
                .AsNoTracking()
                .Where(r => r.IsGlobal ||
                            (r.OwnerId.HasValue && visibleUserIds.Contains(r.OwnerId.Value)) ||
                            (r.AssignedToId.HasValue && visibleUserIds.Contains(r.AssignedToId.Value)));

            if (minCalories.HasValue) query = query.Where(r => r.Calories >= minCalories.Value);
            if (maxCalories.HasValue) query = query.Where(r => r.Calories <= maxCalories.Value);
            if (minProtein.HasValue) query = query.Where(r => r.Protein >= minProtein.Value);
            if (maxProtein.HasValue) query = query.Where(r => r.Protein <= maxProtein.Value);
            if (minCarbs.HasValue) query = query.Where(r => r.Carbs >= minCarbs.Value);
            if (maxCarbs.HasValue) query = query.Where(r => r.Carbs <= maxCarbs.Value);
            if (minFat.HasValue) query = query.Where(r => r.Fat >= minFat.Value);
            if (maxFat.HasValue) query = query.Where(r => r.Fat <= maxFat.Value);

            var results = await query
                .OrderBy(r => r.Title)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Calories,
                    r.Protein,
                    r.Carbs,
                    r.Fat,
                    r.Servings,
                    r.IsGlobal
                })
                .ToListAsync();

            return Ok(results);
        }

        // GET: api/recipes
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var visibleUserIds = await _userContext.GetVisibleUserIdsAsync();

            var recipes = await _db.Recipes
                .Where(r => r.IsGlobal ||
                    (r.OwnerId.HasValue && visibleUserIds.Contains(r.OwnerId.Value)) ||
                    (r.AssignedToId != null && visibleUserIds.Contains(r.AssignedToId.Value)))
                .Include(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.Ingredient)
                .Include(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.Unit)
                .ToListAsync();

            var result = recipes.Select(r => new RecipeDto
            {
                RecipeId = r.Id,
                Title = r.Title,
                Servings = r.Servings,
                Calories = r.Calories,
                Protein = r.Protein,
                Carbs = r.Carbs,
                Fat = r.Fat,
                Ingredients = r.RecipeIngredients.Select(ri => new RecipeIngredientDto
                {
                    Ingredient = ri.Ingredient.Name,
                    Amount = ri.Amount,
                    Unit = ri.Unit?.Code,
                    Notes = ri.Notes
                }).ToList()
            });

            return Ok(result);
        }

        // GET: api/recipes/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var visibleUserIds = await _userContext.GetVisibleUserIdsAsync();

            var recipe = await _db.Recipes
                .Include(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.Ingredient)
                .Include(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.Unit)
                .FirstOrDefaultAsync(r => r.Id == id &&
                    (r.IsGlobal ||
                     (r.OwnerId.HasValue && visibleUserIds.Contains(r.OwnerId.Value)) ||
                     (r.AssignedToId != null && visibleUserIds.Contains(r.AssignedToId.Value))));

            if (recipe == null)
                return NotFound();

            var dto = new RecipeDto
            {
                RecipeId = recipe.Id,
                Title = recipe.Title,
                Servings = recipe.Servings,
                Calories = recipe.Calories,
                Protein = recipe.Protein,
                Carbs = recipe.Carbs,
                Fat = recipe.Fat,
                Ingredients = recipe.RecipeIngredients.Select(ri => new RecipeIngredientDto
                {
                    Ingredient = ri.Ingredient.Name,
                    Amount = ri.Amount,
                    Unit = ri.Unit?.Code,
                    Notes = ri.Notes
                }).ToList()
            };

            return Ok(dto);
        }

        // POST: api/recipes
        [HttpPost]
        public async Task<IActionResult> Create(CreateRecipeDto dto)
        {
            var currentUser = await _userContext.GetCurrentUserAsync();

            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                Servings = dto.Servings,
                Calories = dto.Calories,
                Protein = dto.Protein,
                Carbs = dto.Carbs,
                Fat = dto.Fat,
                MacrosEstimated = false,
                RecipeIngredients = new List<RecipeIngredient>(),
                OwnerId = currentUser.Id,
                IsGlobal = dto.IsGlobal,
                AssignedToId = dto.AssignedUserId
            };

            if (!dto.IsGlobal && dto.AssignedUserId == null)
            {
                recipe.AssignedToId = currentUser.Role == "Client" ? currentUser.Id : null;
            }

            foreach (var i in dto.Ingredients)
            {
                Unit? unit = null;
                if (!string.IsNullOrWhiteSpace(i.Unit))
                {
                    unit = await _db.Units.FirstOrDefaultAsync(u => u.Code == i.Unit);
                }

                var ingredient = await _db.Ingredients
                    .FirstOrDefaultAsync(x => x.Name.ToLower() == i.Ingredient.ToLower());

                if (ingredient == null)
                {
                    ingredient = new Ingredient
                    {
                        Id = Guid.NewGuid(),
                        Name = i.Ingredient
                    };
                    _db.Ingredients.Add(ingredient);
                }

                var recipeIngredient = new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Ingredient = ingredient,
                    Amount = i.Amount,
                    Unit = unit,
                    Notes = i.Notes
                };

                recipe.RecipeIngredients.Add(recipeIngredient);
            }

            _db.Recipes.Add(recipe);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = recipe.Id }, recipe);
        }

        // PUT: api/recipes/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, CreateRecipeDto dto)
        {
            var currentUser = await _userContext.GetCurrentUserAsync();
            var visibleUserIds = await _userContext.GetVisibleUserIdsAsync();

            var recipe = await _db.Recipes
                .Include(r => r.RecipeIngredients)
                .FirstOrDefaultAsync(r => r.Id == id &&
                    (r.IsGlobal ||
                     (r.OwnerId.HasValue && visibleUserIds.Contains(r.OwnerId.Value)) ||
                     (r.AssignedToId != null && visibleUserIds.Contains(r.AssignedToId.Value))));

            if (recipe == null)
                return NotFound();

            if (!recipe.IsGlobal && recipe.OwnerId.HasValue && recipe.OwnerId != currentUser.Id && currentUser.Role != "Master")
                return Forbid();

            recipe.Title = dto.Title;
            recipe.Servings = dto.Servings;
            recipe.Calories = dto.Calories;
            recipe.Protein = dto.Protein;
            recipe.Carbs = dto.Carbs;
            recipe.Fat = dto.Fat;
            recipe.MacrosEstimated = false;
            recipe.IsGlobal = dto.IsGlobal;
            recipe.AssignedToId = dto.AssignedUserId ?? (dto.IsGlobal ? null : recipe.AssignedToId);

            // clear old ingredients
            _db.RecipeIngredients.RemoveRange(recipe.RecipeIngredients);
            recipe.RecipeIngredients.Clear();

            foreach (var i in dto.Ingredients)
            {
                Unit? unit = null;
                if (!string.IsNullOrWhiteSpace(i.Unit))
                {
                    unit = await _db.Units.FirstOrDefaultAsync(u => u.Code == i.Unit);
                }

                var ingredient = await _db.Ingredients
                    .FirstOrDefaultAsync(x => x.Name.ToLower() == i.Ingredient.ToLower());

        if (ingredient == null)
                {
                    ingredient = new Ingredient
                    {
                        Id = Guid.NewGuid(),
                        Name = i.Ingredient
                    };
                    _db.Ingredients.Add(ingredient);
                }

                var recipeIngredient = new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Ingredient = ingredient,
                    Amount = i.Amount,
                    Unit = unit,
                    Notes = i.Notes
                };

                recipe.RecipeIngredients.Add(recipeIngredient);
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/recipes/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var currentUser = await _userContext.GetCurrentUserAsync();
            var recipe = await _db.Recipes.FindAsync(id);
            if (recipe == null)
                return NotFound();

            if (!recipe.IsGlobal && recipe.OwnerId.HasValue && recipe.OwnerId != currentUser.Id && currentUser.Role != "Master")
                return Forbid();

            _db.Recipes.Remove(recipe);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
