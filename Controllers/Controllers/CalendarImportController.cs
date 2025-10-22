using Ical.Net;
using Ical.Net.CalendarComponents;
using Microsoft.AspNetCore.Mvc;
using RecipeApp.Data;
using RecipeApp.Models;
using RecipeApp.Services;

namespace RecipeApp.Controllers;

/// <summary>
/// Imports TrainingPeaks calendars and converts them into structured meal plans.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CalendarImportController : ControllerBase
{
    private readonly AppDb _db;
    private readonly LlmMealPlanParser _llm;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MealPlanAssembler _mealPlanAssembler;
    private readonly ShoppingListBuilder _shoppingListBuilder;

    public CalendarImportController(
        AppDb db,
        LlmMealPlanParser llm,
        IHttpClientFactory httpClientFactory,
        MealPlanAssembler mealPlanAssembler,
        ShoppingListBuilder shoppingListBuilder)
    {
        _db = db;
        _llm = llm;
        _httpClientFactory = httpClientFactory;
        _mealPlanAssembler = mealPlanAssembler;
        _shoppingListBuilder = shoppingListBuilder;
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportCalendar([FromQuery] string? url, [FromQuery] string range = "this")
    {
        Console.WriteLine($"🔹 Importing TrainingPeaks calendar (range={range})");

        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("A TrainingPeaks .ics URL must be provided.");

        string calendarText;
        try
        {
            var client = _httpClientFactory.CreateClient();
            calendarText = await client.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to fetch calendar: {ex.Message}");
        }

        var calendar = Calendar.Load(calendarText);
        var now = DateTime.Now;
        var startOfThisWeek = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).Date;
        var startOfNextWeek = startOfThisWeek.AddDays(7);
        var startDate = range.Equals("next", StringComparison.OrdinalIgnoreCase)
            ? startOfNextWeek
            : startOfThisWeek;
        var endDate = startDate.AddDays(7);

        var events = calendar.Events
            .Where(e => e.Start?.Value != null &&
                        e.Start.Value.Date >= startDate &&
                        e.Start.Value.Date < endDate)
            .ToList();

        if (!events.Any())
            return BadRequest("No events found for the selected range.");

        var parsedPlans = new List<LlmMealPlanParser.ParsedMealPlan>();
        foreach (var dayGroup in events.GroupBy(e => e.Start!.Value.Date))
        {
            var textForDay = string.Join("\n", dayGroup.Select(e => $"{e.Summary}\n{e.Description}"));
            var parsed = await _llm.ParseAsync(textForDay);
            if (parsed != null) parsedPlans.Add(parsed);
        }

        var createdPlans = await _mealPlanAssembler.BuildWeeklyPlansAsync(parsedPlans, startDate);
        _db.MealPlans.AddRange(createdPlans);
        await _db.SaveChangesAsync();

        var recipeIds = createdPlans.SelectMany(p => p.Meals)
            .Where(m => m.RecipeId.HasValue)
            .Select(m => m.RecipeId!.Value)
            .ToList();
        var freeItems = createdPlans.SelectMany(p => p.Meals)
            .Where(m => !string.IsNullOrWhiteSpace(m.FreeText))
            .Select(m => m.FreeText!)
            .ToList();

        var shoppingList = await _shoppingListBuilder.BuildAsync(recipeIds, freeItems);

        Console.WriteLine("✅ Calendar import complete");
        return Ok(new
        {
            Range = range,
            WeekStart = startDate,
            WeekEnd = endDate,
            TotalPlans = createdPlans.Count,
            Plans = createdPlans,
            ShoppingList = shoppingList
        });
    }
}
