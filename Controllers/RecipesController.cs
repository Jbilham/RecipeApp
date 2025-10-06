using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Models;
using RecipeApp.Dtos;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecipesController : ControllerBase
    {
        private readonly AppDb _db;

        public RecipesController(AppDb db)
        {
            _db = db;
        }

        // GET: api/recipes
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var recipes = await _db.Recipes
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
            var recipe = await _db.Recipes
                .Include(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.Ingredient)
                .Include(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.Unit)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
                return NotFound();

            var dto = new RecipeDto
            {
                RecipeId = recipe.Id,
                Title = recipe.Title,
                Servings = recipe.Servings,
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
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                Servings = dto.Servings,
                RecipeIngredients = new List<RecipeIngredient>()
            };

            foreach (var i in dto.Ingredients)
            {
                Unit? unit = null;
                if (!string.IsNullOrWhiteSpace(i.Unit))
                {
                    unit = await _db.Units.FirstOrDefaultAsync(u => u.Code == i.Unit);
                }

                var ingredient = new Ingredient
                {
                    Id = Guid.NewGuid(),
                    Name = i.Ingredient
                };

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
            var recipe = await _db.Recipes
                .Include(r => r.RecipeIngredients)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
                return NotFound();

            recipe.Title = dto.Title;
            recipe.Servings = dto.Servings;

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

                var ingredient = new Ingredient
                {
                    Id = Guid.NewGuid(),
                    Name = i.Ingredient
                };

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
            var recipe = await _db.Recipes.FindAsync(id);
            if (recipe == null)
                return NotFound();

            _db.Recipes.Remove(recipe);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
