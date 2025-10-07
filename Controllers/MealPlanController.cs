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
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<MealPlanDto>> CreateMealPlan([FromForm] CreateMealPlanDto dto)
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
            var weeklyList = await BuildShoppingListAsync(allRecipeIds, allFreeExtras);

            var response = new WeeklyCreateResponse
            {
                Plans = createdPlans.Select(ToDto).ToList(),
                ShoppingList = weeklyList
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

            if (mealPlan is null) return NotFound();

            var recipeIds = mealPlan.Meals
                .Where(m => m.RecipeId.HasValue)
                .Select(m => m.RecipeId!.Value)
                .ToList();

            var freeExtras = ExtractFreeItemsFromMeals(mealPlan.Meals.ToList());
            var list = await BuildShoppingListAsync(recipeIds, freeExtras);
            return Ok(list);
        }

        // ---------- Helpers ----------
        private async Task<List<Meal>> MapParsedMealsAsync(LlmMealPlanParser.ParsedMealPlan parsed)
        {
            var meals = new List<Meal>();
            if (parsed?.Meals is null) return meals;

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

                if (recipeId == null && !string.IsNullOrWhiteSpace(pm.UnmatchedMealTitle))
                    freeText = pm.UnmatchedMealTitle.Trim();

                if (pm.FreeTextItems != null && pm.FreeTextItems.Count > 0)
                {
                    var extras = string.Join(", ", pm.FreeTextItems
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim()));
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

        // ---------- Ingredient Normalisation ----------
        private static string NormalizeIngredientName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            var name = raw.Trim();

            var noise = new[]
            {
                "portion", "portions", "bag", "packet", "leftovers", "dry weight",
                "serve", "served", "medium", "half", "whole", "x", "e.g.", "etc",
                "from", "of", "a", "the"
            };
            foreach (var n in noise)
                name = System.Text.RegularExpressions.Regex.Replace(name, $@"\b{n}\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

            name = System.Text.RegularExpressions.Regex.Replace(name, @"[^\w\s%&'/-]", " ").Trim();
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s{2,}", " ").Trim();

            var irregulars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "tomatoes", "tomato" },
                { "berries", "berry" },
                { "leaves", "leaf" },
                { "potatoes", "potato" },
                { "chopped", "" },
                { "beaten", "" },
                { "sliced", "" },
                { "cooked", "" },
                { "bread", "wholemeal bread" }
            };

            foreach (var kvp in irregulars)
                if (name.EndsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    name = name[..^kvp.Key.Length] + kvp.Value;

            if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
                name.Length > 3)
            {
                name = name[..^1];
            }

            name = name.Trim('-', '_', ' ', '.', ',');
            name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());

            return name;
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

    // 🧩 Add recipe ingredients
    foreach (var ri in rows)
    {
        var key = NormalizeIngredientName(ri.Ingredient.Name);
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
            item.Amount = (item.Amount ?? 0) + (decimal)ri.Amount.Value;

        if (!item.SourceRecipes.Contains(ri.RecipeId))
            item.SourceRecipes.Add(ri.RecipeId);
    }

    // 🧩 Add free-text extras
    foreach (var extra in extraItems)
    {
        if (string.IsNullOrWhiteSpace(extra)) continue;

        var parsed = LlmMealPlanParser.ParseIngredientText(extra);
        var key = NormalizeIngredientName(parsed.name);

        if (!grouped.TryGetValue(key, out var item))
        {
            item = new ShoppingListItemDto
            {
                Ingredient = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key.ToLowerInvariant()),
                Amount = parsed.qty.HasValue ? (decimal?)parsed.qty.Value : null,
                Unit = parsed.unit,
                SourceRecipes = new List<Guid>()
            };
            grouped[key] = item;
        }
        else
        {
            if (parsed.qty.HasValue && (item.Unit == parsed.unit || item.Unit == null))
                item.Amount = (item.Amount ?? 0) + (decimal)parsed.qty.Value;
        }
    }

    // 🧠 Normalize ingredient names with LLM
    var mapping = await _normalizer.NormalizeAsync(grouped.Keys);
    foreach (var kvp in mapping)
    {
        if (grouped.TryGetValue(kvp.Key, out var item))
            item.Ingredient = kvp.Value;
    }

    // 🧹 Remove blanks + merge duplicates
    var cleaned = grouped.Values
        .Where(i => !string.IsNullOrWhiteSpace(i.Ingredient))
        .GroupBy(i => i.Ingredient.Trim(), StringComparer.OrdinalIgnoreCase)
        .Select(g =>
        {
            var first = g.First();
            first.Amount = g.Sum(x => x.Amount ?? 0);
            first.SourceRecipes = g.SelectMany(x => x.SourceRecipes).Distinct().ToList();
            return first;
        })
        .OrderBy(i => i.Ingredient)
        .ToList();

    return new ShoppingListResponse { Items = cleaned };
}


        // ---------- DTO mapping ----------
        [HttpGet]
        public async Task<ActionResult<List<MealPlanDto>>> GetAll()
        {
            var plans = await _db.MealPlans
                .Include(mp => mp.Meals)
                .ThenInclude(m => m.Recipe)
                .AsNoTracking()
                .ToListAsync();

            return Ok(plans.Select(ToDto).ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MealPlanDto>> GetById(Guid id)
        {
            var plan = await _db.MealPlans
                .Include(mp => mp.Meals)
                .ThenInclude(m => m.Recipe)
                .AsNoTracking()
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
