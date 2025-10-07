using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;
using RecipeApp.Services;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MealPlanController : ControllerBase
    {
        private readonly AppDb _db;
        private readonly LlmMealPlanParser _llm;

        public MealPlanController(AppDb db, LlmMealPlanParser llm)
        {
            _db = db;
            _llm = llm;
        }

        // ---------- Single plan ----------

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<MealPlanDto>> CreateMealPlan([FromForm] CreateMealPlanDto dto)
        {
            if (dto is null) return BadRequest("Invalid meal plan.");

            // If caller supplied explicit meals, just persist those.
            if (dto.Meals != null && dto.Meals.Count > 0)
            {
                var planFromClient = new MealPlan
                {
                    Id = Guid.NewGuid(),
                    Name = dto.Name,
                    Date = dto.Date ?? DateTime.UtcNow,
                    Meals = dto.Meals.Select(m => new Meal
                    {
                        Id = Guid.NewGuid(),
                        MealType = m.MealType,
                        RecipeId = m.RecipeId,
                        FreeText = m.FreeText
                    }).ToList()
                };

                _db.MealPlans.Add(planFromClient);
                await _db.SaveChangesAsync();
                return Ok(ToDto(planFromClient));
            }

            // Otherwise parse the free text with the LLM
            var parsed = await _llm.ParseAsync(dto.FreeText ?? "");
            var meals = await MapParsedMealsAsync(parsed);

            var plan = new MealPlan
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Date = dto.Date ?? DateTime.UtcNow,
                Meals = meals
            };

            _db.MealPlans.Add(plan);
            await _db.SaveChangesAsync();

            return Ok(ToDto(plan));
        }

        // ---------- Weekly plan (one MealPlan per day) ----------

        public class WeeklyCreateResponse
        {
            public List<MealPlanDto> Plans { get; set; } = new();
            public ShoppingListResponse ShoppingList { get; set; } = new();
        }

        [HttpPost("week")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<WeeklyCreateResponse>> CreateWeeklyMealPlan([FromForm] CreateWeeklyMealPlanDto dto)
        {
            if (dto is null) return BadRequest("Invalid weekly plan.");

            var weekText = new (string Day, string? Text)[] {
                ("Monday", dto.Monday),
                ("Tuesday", dto.Tuesday),
                ("Wednesday", dto.Wednesday),
                ("Thursday", dto.Thursday),
                ("Friday", dto.Friday),
                ("Saturday", dto.Saturday),
                ("Sunday", dto.Sunday)
            };

            var createdPlans = new List<MealPlan>();
            var allRecipeIds = new List<Guid>();
            var allFreeExtras = new List<string>();

            int offset = 0;
            foreach (var (day, text) in weekText)
            {
                if (string.IsNullOrWhiteSpace(text)) { offset++; continue; }

                var parsed = await _llm.ParseAsync(text);
                var meals = await MapParsedMealsAsync(parsed);

                // collect for weekly shopping list (recipe ingredients + extra free items)
                allRecipeIds.AddRange(meals.Where(m => m.RecipeId.HasValue).Select(m => m.RecipeId!.Value));
                allFreeExtras.AddRange(ExtractFreeItemsFromMeals(meals)); // picked up from FreeText we add below

                var plan = new MealPlan
                {
                    Id = Guid.NewGuid(),
                    Name = $"{dto.Name} - {day}",
                    Date = dto.StartDate.AddDays(offset),
                    Meals = meals
                };

                _db.MealPlans.Add(plan);
                createdPlans.Add(plan);
                offset++;
            }

            await _db.SaveChangesAsync();

            // Build a single combined shopping list for the week
            var weeklyList = await BuildShoppingListAsync(allRecipeIds, allFreeExtras);

            var response = new WeeklyCreateResponse
            {
                Plans = createdPlans.Select(ToDto).ToList(),
                ShoppingList = weeklyList
            };

            return Ok(response);

            // If you need to preserve old return shape (list only), use:
            // return Ok(createdPlans.Select(ToDto).ToList());
        }

[HttpPost("week/preview")]
[Consumes("multipart/form-data")]
public async Task<ActionResult<WeeklyCreateResponse>> PreviewWeeklyMealPlan([FromForm] CreateWeeklyMealPlanDto dto)
{
    if (dto is null) return BadRequest("Invalid weekly plan.");

    var weekText = new (string Day, string? Text)[] {
        ("Monday", dto.Monday),
        ("Tuesday", dto.Tuesday),
        ("Wednesday", dto.Wednesday),
        ("Thursday", dto.Thursday),
        ("Friday", dto.Friday),
        ("Saturday", dto.Saturday),
        ("Sunday", dto.Sunday)
    };

    var allPlans = new List<MealPlan>();
    var allRecipeIds = new List<Guid>();
    var allExtras = new List<string>();

    int offset = 0;
    foreach (var (day, text) in weekText)
    {
        if (string.IsNullOrWhiteSpace(text)) { offset++; continue; }

        var parsed = await _llm.ParseAsync(text);
        var meals = await MapParsedMealsAsync(parsed);

        var plan = new MealPlan
        {
            Id = Guid.NewGuid(),
            Name = $"{dto.Name} - {day}",
            Date = dto.StartDate.AddDays(offset),
            Meals = meals,
            FreeItems = ExtractFreeItemsFromMeals(meals)
        };

            allPlans.Add(plan);
            allRecipeIds.AddRange(meals.Where(m => m.RecipeId.HasValue).Select(m => m.RecipeId!.Value));
            allExtras.AddRange(plan.FreeItems);
            offset++;
            }

            var shopping = await BuildShoppingListAsync(allRecipeIds, allExtras);

            return Ok(new WeeklyCreateResponse
            {
                Plans = allPlans.Select(ToDto).ToList(),
                ShoppingList = shopping
            });
        }

        

        // ---------- Shopping list (per plan) ----------

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

            if (mealPlan is null) return NotFound();

            var recipeIds = mealPlan.Meals
                .Where(m => m.RecipeId.HasValue)
                .Select(m => m.RecipeId!.Value)
                .ToList();

            var freeExtras = ExtractFreeItemsFromMeals(mealPlan.Meals.ToList());

            var list = await BuildShoppingListAsync(recipeIds, freeExtras);
            return Ok(list);
        }

        // ---------- Lookups & helpers ----------

        /// Map LLM parsed meals into persisted Meals.
        /// - If MatchedRecipeTitle matches a Recipe, set RecipeId.
        /// - If no match, put title into FreeText.
        /// - Append FreeTextItems to FreeText (comma-separated) so your shopping list
        ///   logic keeps working without schema changes.
        private async Task<List<Meal>> MapParsedMealsAsync(LlmMealPlanParser.ParsedMealPlan parsed)
        {
            var meals = new List<Meal>();
            if (parsed?.Meals is null) return meals;

            // Preload titles once to reduce queries
            var allRecipes = await _db.Recipes
                .AsNoTracking()
                .Select(r => new { r.Id, r.Title })
                .ToListAsync();

            foreach (var pm in parsed.Meals)
            {
                Guid? recipeId = null;
                string? freeText = null;

                if (!string.IsNullOrWhiteSpace(pm.MatchedRecipeTitle))
                {
                    var match = allRecipes.FirstOrDefault(r =>
                        string.Equals(r.Title, pm.MatchedRecipeTitle, StringComparison.OrdinalIgnoreCase));
                    if (match != null) recipeId = match.Id;
                }

                if (recipeId == null)
                {
                    // Use the unmatched title (if present) as the meal text
                    if (!string.IsNullOrWhiteSpace(pm.UnmatchedMealTitle))
                        freeText = pm.UnmatchedMealTitle.Trim();
                }

                // Append parser free-text items (extras) so the shopping-list endpoint can pick them up
                if (pm.FreeTextItems != null && pm.FreeTextItems.Count > 0)
                {
                    var extras = string.Join(", ", pm.FreeTextItems.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
                    if (!string.IsNullOrWhiteSpace(extras))
                        freeText = string.IsNullOrWhiteSpace(freeText) ? extras : $"{freeText}, {extras}";
                }

                meals.Add(new Meal
                {
                    Id = Guid.NewGuid(),
                    MealType = pm.MealType ?? "Meal",
                    RecipeId = recipeId,
                    FreeText = freeText
                });
            }

            return meals;
        }

        /// Extracts extras from Meal.FreeText (comma/semicolon/newline delimited).
        private static List<string> ExtractFreeItemsFromMeals(List<Meal> meals)
        {
            var extras = new List<string>();
            foreach (var m in meals)
            {
                if (string.IsNullOrWhiteSpace(m.FreeText)) continue;
                var parts = m.FreeText
                    .Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                // We only want "extra items", not the dish name, but since we
                // appended extras to FreeText and dish name is often first,
                // this simple approach works well enough for now.
                extras.AddRange(parts);
            }
            return extras;
        }

        /// Builds a shopping list from recipe ingredients + extra free items.
        private async Task<ShoppingListResponse> BuildShoppingListAsync(List<Guid> recipeIds, List<string> extraItems)
        {
            var rows = await _db.RecipeIngredients
                .Include(ri => ri.Ingredient)
                .Include(ri => ri.Unit)
                .Where(ri => recipeIds.Contains(ri.RecipeId))
                .ToListAsync();

            // Aggregate recipe ingredients
            var grouped = new Dictionary<string, ShoppingListItemDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var ri in rows)
            {
                var key = ri.Ingredient.Name.Trim();
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

                if (ri.Amount.HasValue) item.Amount = (item.Amount ?? 0) + ri.Amount.Value;
                if (!item.SourceRecipes.Contains(ri.RecipeId)) item.SourceRecipes.Add(ri.RecipeId);
            }

            // Add extras (no unit/amount)
            foreach (var extra in extraItems)
            {
                var key = extra.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;
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

            return new ShoppingListResponse
            {
                Items = grouped.Values.OrderBy(i => i.Ingredient).ToList()
            };
        }

        // ---------- DTO mapping (unchanged) ----------

        [HttpGet]
        public async Task<ActionResult<List<MealPlanDto>>> GetAll()
        {
            var plans = await _db.MealPlans
                .Include(mp => mp.Meals)
                .ThenInclude(m => m.Recipe)
                .ToListAsync();

            return Ok(plans.Select(ToDto).ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MealPlanDto>> GetById(Guid id)
        {
            var plan = await _db.MealPlans
                .Include(mp => mp.Meals)
                .ThenInclude(m => m.Recipe)
                .FirstOrDefaultAsync(mp => mp.Id == id);

            if (plan == null) return NotFound();
            return Ok(ToDto(plan));
        }

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
