using System.Globalization;
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
        private readonly LlmIngredientNormalizer _normalizer;

        public MealPlanController(AppDb db, LlmMealPlanParser llm, LlmIngredientNormalizer normalizer)
        {
            _db = db;
            _llm = llm;
            _normalizer = normalizer;
        }

        // ---------- Single plan ----------
        [HttpPost]
        [Consumes("application/json")]
        public async Task<ActionResult<MealPlanDto>> CreateMealPlan([FromBody] CreateMealPlanDto dto)
        {
            if (dto is null) return BadRequest("Invalid meal plan.");

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

            var parsed = await _llm.ParseAsync(dto.FreeText ?? "");
            var meals = await MapParsedMealsAsync(parsed ?? new LlmMealPlanParser.ParsedMealPlan());

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

        // ---------- Weekly plan ----------
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

            var weekText = new (string Day, string? Text)[]
            {
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

                var parsed = await _llm.ParseAsync(text ?? "");
                var meals = await MapParsedMealsAsync(parsed ?? new LlmMealPlanParser.ParsedMealPlan());
                allRecipeIds.AddRange(meals.Where(m => m.RecipeId.HasValue).Select(m => m.RecipeId!.Value));
                allFreeExtras.AddRange(ExtractFreeItemsFromMeals(meals));

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

            // ✅ Re-load plans with recipes attached
            var planIds = createdPlans.Select(p => p.Id).ToList();
            var refreshedPlans = await _db.MealPlans
                .Where(p => planIds.Contains(p.Id))
                .Include(p => p.Meals)
                    .ThenInclude(m => m.Recipe)
                .AsNoTracking()
                .ToListAsync();

            // 🛒 Build shopping list
            var weeklyList = await BuildShoppingListAsync(allRecipeIds, allFreeExtras);

            // 🧾 Save snapshot for recall
            var snapshot = new ShoppingListSnapshot
            {
                Id = Guid.NewGuid(),
                MealPlanId = null,
                JsonData = System.Text.Json.JsonSerializer.Serialize(weeklyList),
                SourceType = "weekly",
                CreatedAt = DateTime.UtcNow
            };

            _db.ShoppingListSnapshots.Add(snapshot);
            await _db.SaveChangesAsync();

            // ✅ Build response using refreshed plans (with Recipe.Title)
            var response = new
            {
                Plans = refreshedPlans.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Date,
                    Meals = p.Meals.Select(m => new
                    {
                        m.Id,
                        m.MealType,
                        m.RecipeId,
                        RecipeName = m.RecipeId.HasValue
                            ? (m.Recipe?.Title ?? "(Recipe missing from DB)")
                            : "Unknown",
                        m.FreeText
                    }),
                    p.FreeItems
                }),
                ShoppingList = weeklyList,
                SnapshotId = snapshot.Id
            };

            return Ok(response);
        }

        [HttpPost("week/preview")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<WeeklyCreateResponse>> PreviewWeeklyMealPlan([FromForm] CreateWeeklyMealPlanDto dto)
        {
            if (dto is null) return BadRequest("Invalid weekly plan.");

            var weekText = new (string Day, string? Text)[]
            {
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

                var parsed = await _llm.ParseAsync(text ?? "");
                var meals = await MapParsedMealsAsync(parsed ?? new LlmMealPlanParser.ParsedMealPlan());

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

        // ---------- Shopping list ----------
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

            if (mealPlan is null)
                return NotFound();

            var recipeIds = mealPlan.Meals
                .Where(m => m.RecipeId.HasValue)
                .Select(m => m.RecipeId!.Value)
                .ToList();

            var freeExtras = ExtractFreeItemsFromMeals(mealPlan.Meals.ToList());
            var list = await BuildShoppingListAsync(recipeIds, freeExtras);

            var snapshot = new ShoppingListSnapshot
            {
                Id = Guid.NewGuid(),
                MealPlanId = id,
                JsonData = System.Text.Json.JsonSerializer.Serialize(list),
                SourceType = "single",
                CreatedAt = DateTime.UtcNow
            };

            _db.ShoppingListSnapshots.Add(snapshot);
            await _db.SaveChangesAsync();

            return Ok(list);
        }

        // ---------- Helpers ----------
        private async Task<List<Meal>> MapParsedMealsAsync(LlmMealPlanParser.ParsedMealPlan parsed)
        {
            var meals = new List<Meal>();
            if (parsed?.Meals is null) return meals;

            string Normalize(string input)
            {
                return input.ToLower()
                    .Replace("&", "and")
                    .Replace("-", " ")
                    .Replace("  ", " ")
                    .Trim();
            }

            var allRecipes = await _db.Recipes
                .AsNoTracking()
                .Select(r => new { r.Id, r.Title })
                .ToListAsync();

            foreach (var pm in parsed.Meals)
            {
                string? normalizedName = pm.MatchedRecipeTitle ?? pm.UnmatchedMealTitle;
                Recipe? matchedRecipe = null;

                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    var normalizedSearch = Normalize(normalizedName);

                    matchedRecipe = await _db.Recipes
                        .FirstOrDefaultAsync(r => EF.Functions.ILike(r.Title, $"%{normalizedSearch}%"));

                    if (matchedRecipe == null)
                    {
                        matchedRecipe = allRecipes
                            .Select(r => new Recipe { Id = r.Id, Title = r.Title })
                            .FirstOrDefault(r =>
                                Normalize(r.Title).Contains(normalizedSearch) ||
                                normalizedSearch.Contains(Normalize(r.Title)));
                    }

                    if (matchedRecipe == null)
                    {
                        var words = normalizedSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        matchedRecipe = allRecipes
                            .Select(r => new Recipe { Id = r.Id, Title = r.Title })
                            .FirstOrDefault(r =>
                            {
                                var titleWords = Normalize(r.Title).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                return words.Intersect(titleWords).Count() >= Math.Max(1, words.Length / 2);
                            });
                    }
                }

                string? combinedFreeText = null;
                if (pm.FreeTextItems != null && pm.FreeTextItems.Any())
                    combinedFreeText = string.Join(", ", pm.FreeTextItems);

                meals.Add(new Meal
                {
                    Id = Guid.NewGuid(),
                    MealType = pm.MealType ?? "Meal",
                    RecipeId = matchedRecipe?.Id,
                    FreeText = matchedRecipe == null
                        ? $"{normalizedName ?? combinedFreeText ?? "Meal"} (Recipe Unknown)"
                        : combinedFreeText
                });
            }

            return meals;
        }

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
                extras.AddRange(parts);
            }
            return extras;
        }

        private async Task<ShoppingListResponse> BuildShoppingListAsync(List<Guid> recipeIds, List<string> extraItems)
        {
            var rows = await _db.RecipeIngredients
                .Include(ri => ri.Ingredient)
                .Include(ri => ri.Unit)
                .Where(ri => recipeIds.Contains(ri.RecipeId))
                .AsNoTracking()
                .ToListAsync();

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

                if (ri.Amount.HasValue)
                    item.Amount = (item.Amount ?? 0) + ri.Amount.Value;

                if (!item.SourceRecipes.Contains(ri.RecipeId))
                    item.SourceRecipes.Add(ri.RecipeId);
            }

            foreach (var extra in extraItems)
            {
                if (string.IsNullOrWhiteSpace(extra)) continue;
                if (!grouped.ContainsKey(extra))
                    grouped[extra] = new ShoppingListItemDto
                    {
                        Ingredient = extra,
                        Amount = null,
                        Unit = null,
                        SourceRecipes = new List<Guid>()
                    };
            }

            return new ShoppingListResponse { Items = grouped.Values.ToList() };
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
