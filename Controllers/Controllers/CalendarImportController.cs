using IcalNetCalendar = Ical.Net.Calendar;
using Ical.Net.CalendarComponents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;
using RecipeApp.Services;
using System.Globalization;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CalendarImportController : ControllerBase
    {
        private readonly AppDb _db;
        private readonly LlmMealPlanParser _llm;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CalendarImportController> _logger;

        public CalendarImportController(
            AppDb db,
            LlmMealPlanParser llm,
            IHttpClientFactory httpClientFactory,
            ILogger<CalendarImportController> logger)
        {
            _db = db;
            _llm = llm;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Imports a TrainingPeaks .ics calendar, parses each day's nutrition plan,
        /// and creates corresponding MealPlans in the database.
        /// </summary>
        /// <param name="url">Optional .ics URL (e.g. https://www.trainingpeaks.com/ical/XXXX.ics)</param>
        /// <param name="range">
        /// "this" or "next" week, or a specific start date (YYYY-MM-DD).
        /// </param>
        /// <param name="file">Optional .ics file upload</param>
        [HttpPost("import")]
        public async Task<IActionResult> ImportCalendar(
            [FromQuery] string? url = null,
            [FromQuery] string range = "this",
            IFormFile? file = null)
        {
            string calendarText;

            // --- 1️⃣ Load calendar text (URL or file) ---
            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(15);
                    calendarText = await client.GetStringAsync(url);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch TrainingPeaks .ics file from {Url}", url);
                    return BadRequest($"Failed to fetch calendar from URL: {ex.Message}");
                }
            }
            else if (file != null && file.Length > 0)
            {
                using var reader = new StreamReader(file.OpenReadStream());
                calendarText = await reader.ReadToEndAsync();
            }
            else
            {
                return BadRequest("No calendar source provided. Provide ?url= or upload a .ics file.");
            }

            if (string.IsNullOrWhiteSpace(calendarText))
                return BadRequest("The calendar file appears to be empty or invalid.");

            // --- 2️⃣ Parse calendar ---
            IcalNetCalendar? calendar;
            try
            {
                calendar = IcalNetCalendar.Load(calendarText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse .ics calendar.");
                return BadRequest($"Could not parse calendar: {ex.Message}");
            }

            if (calendar == null)
                return BadRequest("Could not parse calendar file (no valid VEVENT entries found).");

            // --- 3️⃣ Determine target week or custom range ---
            var now = DateTime.Now;
            var (startDate, endDate) = ResolveRange(range, now);

            // --- 4️⃣ Filter relevant events ---
            var events = calendar.Events
                .Where(e => e.Start?.Value != null)
                .Where(e =>
                    e.Start!.Value.Date >= startDate &&
                    e.Start!.Value.Date < endDate)
                .ToList();

            if (!events.Any())
                return BadRequest($"No events found for selected range ({range}).");

            // --- 5️⃣ Parse and create MealPlans ---
            var createdPlans = new List<MealPlan>();
            var allRecipeIds = new List<Guid>();
            var allFreeExtras = new List<string>();

            var groupedByDay = events
                .GroupBy(e => e.Start!.Value.Date)
                .OrderBy(g => g.Key);

            foreach (var dayGroup in groupedByDay)
            {
                var date = dayGroup.Key;
                var dayName = date.ToString("dddd", CultureInfo.InvariantCulture);

                // Combine daily event text
                var textForDay = string.Join("\n", dayGroup.Select(e =>
                    $"{e.Summary}\n{e.Description?.Replace("\\n", "\n")}"));

                LlmMealPlanParser.ParsedMealPlan? parsed = null;
                try
                {
                    parsed = await _llm.ParseAsync(textForDay);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LLM parsing failed for {Date}.", date);
                }

                var meals = await MapParsedMealsAsync(parsed ?? new LlmMealPlanParser.ParsedMealPlan());

                allRecipeIds.AddRange(meals.Where(m => m.RecipeId.HasValue).Select(m => m.RecipeId!.Value));
                allFreeExtras.AddRange(ExtractFreeItemsFromMeals(meals));

                var plan = new MealPlan
                {
                    Id = Guid.NewGuid(),
                    Name = $"Imported Plan - {dayName}",
                    Date = date,
                    Meals = meals
                };

                _db.MealPlans.Add(plan);
                createdPlans.Add(plan);
            }

            await _db.SaveChangesAsync();

            // --- 6️⃣ Build Shopping List ---
            var shoppingList = await BuildShoppingListAsync(allRecipeIds, allFreeExtras);

            return Ok(new
            {
                Range = range,
                WeekStart = startDate.ToString("yyyy-MM-dd"),
                WeekEnd = endDate.ToString("yyyy-MM-dd"),
                TotalPlans = createdPlans.Count,
                Plans = createdPlans.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Date,
                    Meals = p.Meals.Select(m => new
                    {
                        m.MealType,
                        m.RecipeId,
                        RecipeName = m.RecipeId.HasValue
                            ? (m.Recipe?.Title ?? "(Recipe missing)")
                            : m.FreeText ?? "(No recipe available)",
                        m.FreeText
                    })
                }),
                ShoppingList = shoppingList
            });
        }

        // ------------------------
        // Helpers
        // ------------------------

        private static (DateTime start, DateTime end) ResolveRange(string range, DateTime now)
        {
            if (DateTime.TryParse(range, out var manualStart))
            {
                return (manualStart.Date, manualStart.Date.AddDays(7));
            }

            // ISO week (Monday start)
            var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfThisWeek = now.AddDays(-diff).Date;
            var startOfNextWeek = startOfThisWeek.AddDays(7);

            var start = range.Equals("next", StringComparison.OrdinalIgnoreCase)
                ? startOfNextWeek
                : startOfThisWeek;
            var end = start.AddDays(7);

            return (start, end);
        }

        private async Task<List<Meal>> MapParsedMealsAsync(LlmMealPlanParser.ParsedMealPlan parsed)
        {
            var meals = new List<Meal>();
            if (parsed?.Meals == null) return meals;

            string Normalize(string input) =>
                input.ToLowerInvariant()
                     .Replace("&", "and")
                     .Replace("-", " ")
                     .Replace("  ", " ")
                     .Trim();

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
                        .FirstOrDefaultAsync(r => EF.Functions.ILike(r.Title, $"%{normalizedSearch}%"))
                        ?? allRecipes
                            .Select(r => new Recipe { Id = r.Id, Title = r.Title })
                            .FirstOrDefault(r =>
                                Normalize(r.Title).Contains(normalizedSearch) ||
                                normalizedSearch.Contains(Normalize(r.Title)));
                }

                string? combinedFreeText = pm.FreeTextItems != null && pm.FreeTextItems.Any()
                    ? string.Join(", ", pm.FreeTextItems)
                    : null;

                meals.Add(new Meal
                {
                    Id = Guid.NewGuid(),
                    MealType = pm.MealType ?? "Meal",
                    RecipeId = matchedRecipe?.Id,
                    FreeText = matchedRecipe == null
                        ? normalizedName ?? combinedFreeText ?? "Meal"
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
                    .Split(new[] { '\n', ',', ';', '+' }, StringSplitOptions.RemoveEmptyEntries)
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
    }
}
