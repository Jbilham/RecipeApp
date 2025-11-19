using Microsoft.AspNetCore.Mvc;
using RecipeApp.Data;
using RecipeApp.Services;
using System.Globalization;

// alias to avoid ambiguity
using IcalNetCalendar = Ical.Net.Calendar;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CalendarImportController : ControllerBase
    {
        private readonly AppDb _db;
        private readonly LlmMealPlanParser _llm;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly MealPlanAssembler _assembler;
        private readonly ShoppingListBuilder _shopping;

        public CalendarImportController(
            AppDb db,
            LlmMealPlanParser llm,
            IHttpClientFactory httpClientFactory,
            MealPlanAssembler assembler,
            ShoppingListBuilder shopping)
        {
            _db = db;
            _llm = llm;
            _httpClientFactory = httpClientFactory;
            _assembler = assembler;
            _shopping = shopping;
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportCalendar(
            [FromQuery] string url,
            [FromQuery] string range = "this")
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest("No calendar URL provided.");

            url = url.Trim();
            if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url.Substring("webcal://".Length);
                Console.WriteLine($"🌐 Normalized webcal URL → {url}");
            }

            string calendarText;
            try
            {
                var client = _httpClientFactory.CreateClient();
                calendarText = await client.GetStringAsync(url);
                Console.WriteLine($"📄 Downloaded calendar ({calendarText.Length} chars)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to fetch .ics: {ex.Message}");
                return BadRequest($"Failed to fetch calendar: {ex.Message}");
            }

            var calendar = IcalNetCalendar.Load(calendarText);
            if (calendar?.Events == null || !calendar.Events.Any())
                return Ok(new { message = "No events found." });

            var now = DateTime.Now;
            var startOfThisWeek = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).Date;
            var startDate = range.Equals("next", StringComparison.OrdinalIgnoreCase)
                ? startOfThisWeek.AddDays(7)
                : startOfThisWeek;
            var endDate = startDate.AddDays(7);

            Console.WriteLine($"📅 Importing range: {range} ({startDate:yyyy-MM-dd} → {endDate:yyyy-MM-dd})");

            var events = calendar.Events
                .Where(e => e.Start?.Value != null &&
                            e.Start.Value.Date >= startDate &&
                            e.Start.Value.Date < endDate)
                .OrderBy(e => e.Start.Value.Date)
                .ToList();

            var groupedByDay = events.GroupBy(e => e.Start!.Value.Date).OrderBy(g => g.Key);

            var createdPlans = new List<Models.MealPlan>();
            var allRecipeIds = new List<Guid>();
            var allFreeExtras = new List<string>();

            foreach (var dayGroup in groupedByDay)
            {
                var date = DateTime.SpecifyKind(dayGroup.Key, DateTimeKind.Unspecified);
                var dayName = date.ToString("dddd", CultureInfo.InvariantCulture);
                var textForDay = string.Join("\n", dayGroup.Select(e => $"{e.Summary}\n{e.Description}".Trim()));

                Console.WriteLine($"🧩 Parsing day {dayName} ({date:yyyy-MM-dd}), text length={textForDay.Length}");
                var parsed = await _llm.ParseAsync(textForDay);
                if (parsed == null)
                {
                    Console.WriteLine($"⚠️ No valid meals found for {dayName}");
                    continue;
                }

                var plan = await _assembler.CreateMealPlanFromParsedAsync(parsed, date, $"Imported Plan - {dayName}");
                createdPlans.Add(plan);

                allRecipeIds.AddRange(plan.Meals.Where(m => m.RecipeId.HasValue).Select(m => m.RecipeId!.Value));
                allFreeExtras.AddRange(plan.Meals.Where(m => !string.IsNullOrWhiteSpace(m.FreeText)).Select(m => m.FreeText!));
            }

            await _db.SaveChangesAsync();

            var shoppingList = await _shopping.BuildAsync(allRecipeIds, allFreeExtras);

            Console.WriteLine($"✅ Imported {createdPlans.Count} plans successfully.");

            return Ok(new
            {
                range,
                weekStart = startDate,
                weekEnd = endDate,
                totalPlans = createdPlans.Count,
                plans = createdPlans.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Date,
                    Meals = p.Meals.Select(m => new { m.MealType, m.RecipeId, m.FreeText })
                }),
                shoppingList
            });
        }
    }
}