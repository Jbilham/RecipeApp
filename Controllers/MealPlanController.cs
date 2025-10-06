using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;
using RecipeApp.Helpers;
using RecipeApp.Services;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MealPlanController : ControllerBase
    {
        private readonly AppDb _db;
        private readonly LlmMealPlanParser _llmParser;

        public MealPlanController(AppDb db, LlmMealPlanParser llmParser)
        {
            _db = db;
            _llmParser = llmParser;
        }

        // ✅ Create a single meal plan (existing)
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<MealPlanDto>> CreateMealPlan([FromForm] CreateMealPlanDto dto)
        {
            if (dto == null) return BadRequest("Invalid meal plan.");

            var parsedMeals = await _llmParser.ParseAsync(dto.FreeText ?? "");

            var plan = new MealPlan
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Date = dto.Date ?? DateTime.UtcNow,
                Meals = parsedMeals.Meals.Select(m => new Meal
                {
                    Id = Guid.NewGuid(),
                    MealType = m.MealType,
                    RecipeId = m.RecipeId,
                    FreeText = m.FreeText
                }).ToList()
            };

            _db.MealPlans.Add(plan);
            await _db.SaveChangesAsync();

            return ToDto(plan);
        }

        // ✅ NEW: Create a full week’s meal plans in one call
        [HttpPost("week")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<IEnumerable<MealPlanDto>>> CreateWeeklyMealPlan([FromForm] CreateWeeklyMealPlanDto dto)
        {
            if (dto == null) return BadRequest("Invalid weekly plan.");

            var resultPlans = new List<MealPlan>();

            var weekDays = new Dictionary<string, string?>
            {
                { "Monday", dto.Monday },
                { "Tuesday", dto.Tuesday },
                { "Wednesday", dto.Wednesday },
                { "Thursday", dto.Thursday },
                { "Friday", dto.Friday },
                { "Saturday", dto.Saturday },
                { "Sunday", dto.Sunday }
            };

            int dayOffset = 0;

            foreach (var (day, text) in weekDays)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    dayOffset++;
                    continue;
                }

                var parsedMeals = await _llmParser.ParseAsync(text);

                var mealPlan = new MealPlan
                {
                    Id = Guid.NewGuid(),
                    Name = $"{dto.Name} - {day}",
                    Date = dto.StartDate.AddDays(dayOffset),
                    Meals = parsedMeals.Meals.Select(m => new Meal
                    {
                        Id = Guid.NewGuid(),
                        MealType = m.MealType,
                        RecipeId = m.RecipeId,
                        FreeText = m.FreeText
                    }).ToList()
                };

                _db.MealPlans.Add(mealPlan);
                resultPlans.Add(mealPlan);
                dayOffset++;
            }

            await _db.SaveChangesAsync();
            return resultPlans.Select(ToDto).ToList();
        }

        // ✅ Get all meal plans
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MealPlanDto>>> GetAll()
        {
            var plans = await _db.MealPlans
                .Include(mp => mp.Meals)
                .ThenInclude(m => m.Recipe)
                .ToListAsync();

            return plans.Select(ToDto).ToList();
        }

        // ✅ Get a single meal plan by ID
        [HttpGet("{id}")]
        public async Task<ActionResult<MealPlanDto>> GetById(Guid id)
        {
            var plan = await _db.MealPlans
                .Include(mp => mp.Meals)
                .ThenInclude(m => m.Recipe)
                .FirstOrDefaultAsync(mp => mp.Id == id);

            if (plan == null) return NotFound();

            return ToDto(plan);
        }

        // ✅ Shopping list from a meal plan
        [HttpGet("{id}/shopping-list")]
        public async Task<ActionResult<ShoppingListResponse>> GetShoppingList(Guid id)
        {
            var mealPlan = await _db.MealPlans
                .Include(mp => mp.Meals)
                .ThenInclude(m => m.Recipe)
                .ThenInclude(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
                .Include(mp => mp.Meals)
                .ThenInclude(m => m.Recipe)
                .ThenInclude(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Unit)
                .FirstOrDefaultAsync(mp => mp.Id == id);

            if (mealPlan == null) return NotFound();

            var recipeIds = mealPlan.Meals
                .Where(m => m.RecipeId.HasValue)
                .Select(m => m.RecipeId.Value)
                .ToList();

            var freeItems = mealPlan.Meals
                .Where(m => !string.IsNullOrWhiteSpace(m.FreeText))
                .SelectMany(m => m.FreeText!
                    .Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()))
                .ToList();

            var rows = await _db.RecipeIngredients
                .Include(ri => ri.Ingredient)
                .Include(ri => ri.Unit)
                .Where(ri => recipeIds.Contains(ri.RecipeId))
                .ToListAsync();

            var grouped = new Dictionary<string, ShoppingListItemDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var ri in rows)
            {
                var name = ri.Ingredient.Name;

                if (!grouped.TryGetValue(name, out var item))
                {
                    item = new ShoppingListItemDto
                    {
                        Ingredient = ri.Ingredient.Name,
                        Amount = 0,
                        Unit = ri.Unit?.Code,
                        SourceRecipes = new List<Guid>()
                    };
                    grouped[name] = item;
                }

                if (ri.Amount.HasValue)
                    item.Amount = (item.Amount ?? 0) + ri.Amount.Value;

                if (!item.SourceRecipes.Contains(ri.RecipeId))
                    item.SourceRecipes.Add(ri.RecipeId);
            }

            foreach (var free in freeItems)
            {
                if (!grouped.ContainsKey(free))
                {
                    grouped[free] = new ShoppingListItemDto
                    {
                        Ingredient = free,
                        Amount = null,
                        Unit = null,
                        SourceRecipes = new List<Guid>()
                    };
                }
            }

            return new ShoppingListResponse
            {
                Items = grouped.Values.OrderBy(i => i.Ingredient).ToList()
            };
        }

        // ✅ Helper: convert entity -> DTO
        private static MealPlanDto ToDto(MealPlan plan)
        {
            return new MealPlanDto
            {
                Id = plan.Id,
                Name = plan.Name,
                Date = plan.Date ?? DateTime.MinValue,
                Meals = plan.Meals?.Select(m => new MealDto
                {
                    Id = m.Id,
                    MealType = m.MealType,
                    RecipeId = m.RecipeId,
                    RecipeName = m.Recipe?.Title ?? "Unknown",
                    FreeText = m.FreeText
                }).ToList() ?? new List<MealDto>()
            };
        }
    }
}
