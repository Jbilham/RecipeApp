using Ical.Net.CalendarComponents;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Helpers;
using RecipeApp.Models;
using System.Globalization;
using System.Text.Json;
using IcalNetCalendar = Ical.Net.Calendar;

namespace RecipeApp.Services
{
    public interface ICalendarImportService
    {
        Task<CalendarImportResult> ImportAsync(AppUser user, string url, string range);
    }

    public class CalendarImportService : ICalendarImportService
    {
        private readonly AppDb _db;
        private readonly LlmMealPlanParser _llm;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly MealPlanAssembler _assembler;
        private readonly ShoppingListBuilder _shopping;

        public CalendarImportService(
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

        public async Task<CalendarImportResult> ImportAsync(AppUser user, string url, string range)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("No calendar URL provided.", nameof(url));
            }

            var normalizedUrl = NormalizeUrl(url.Trim());

            string calendarText;
            try
            {
                var client = _httpClientFactory.CreateClient();
                calendarText = await client.GetStringAsync(normalizedUrl);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to fetch calendar: {ex.Message}", ex);
            }

            var calendar = IcalNetCalendar.Load(calendarText);
            if (calendar?.Events == null || !calendar.Events.Any())
            {
                throw new InvalidOperationException("No events found in the supplied calendar.");
            }

            var today = DateTime.Now.Date;
            var diffToMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var startOfThisWeek = today.AddDays(-diffToMonday);
            var startDate = range.Equals("next", StringComparison.OrdinalIgnoreCase)
                ? startOfThisWeek.AddDays(7)
                : startOfThisWeek;
            var endDate = startDate.AddDays(7);

            var events = calendar.Events
                .Where(e => e?.Start?.Value != null)
                .Select(e => new
                {
                    Event = e,
                    Date = e.Start!.Value.Date
                })
                .Where(e => e.Date >= startDate && e.Date < endDate)
                .OrderBy(e => e.Date)
                .ToList();

            var groupedByDay = events.GroupBy(e => e.Date).OrderBy(g => g.Key);

            var createdPlans = new List<MealPlan>();
            var planDtos = new List<CalendarImportPlanDto>();
            var missingMealsDtos = new List<CalendarImportMissingMealDto>();
            var allRecipeIds = new List<Guid>();
            var allFreeExtras = new List<string>();

            foreach (var dayGroup in groupedByDay)
            {
                var dateLocal = dayGroup.Key;
                var dayName = dateLocal.ToString("dddd", CultureInfo.InvariantCulture);
                var textForDay = string.Join("\n", dayGroup.Select(e =>
                {
                    var summary = e.Event.Summary ?? string.Empty;
                    var description = e.Event.Description ?? string.Empty;
                    return $"{summary}\n{description}".Trim();
                }).Where(t => !string.IsNullOrWhiteSpace(t)));

                if (string.IsNullOrWhiteSpace(textForDay))
                {
                    continue;
                }

                var parsed = await _llm.ParseAsync(textForDay);
                if (parsed == null)
                {
                    continue;
                }

                var plan = await _assembler.CreateMealPlanFromParsedAsync(parsed, dateLocal, $"Imported Plan - {dayName}");
                plan.CreatedById = user.Id;
                plan.AssignedToId ??= user.Id;
                createdPlans.Add(plan);

                var selectedMealsForPlan = plan.Meals.Where(m => m.IsSelected).ToList();
                allRecipeIds.AddRange(selectedMealsForPlan.Where(m => m.RecipeId.HasValue).Select(m => m.RecipeId!.Value));

                var extrasForPlan = selectedMealsForPlan
                    .SelectMany(m => m.ExtraItems)
                    .ToList();
                if (extrasForPlan.Count > 0)
                {
                    plan.FreeItems = extrasForPlan;
                    allFreeExtras.AddRange(extrasForPlan);
                }

                planDtos.Add(new CalendarImportPlanDto
                {
                    Id = plan.Id,
                    Name = plan.Name,
                    Date = plan.Date,
                    Meals = new List<CalendarImportMealDto>()
                });
            }

            await _db.SaveChangesAsync();

            var shoppingList = await _shopping.BuildAsync(allRecipeIds, allFreeExtras);

            var weekStartUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var weekEndUtc = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

            var mealPlanSnapshot = new MealPlanSnapshot
            {
                CreatedAt = DateTime.UtcNow,
                WeekStart = weekStartUtc,
                WeekEnd = weekEndUtc,
                Range = range,
                SourceType = "calendar-import",
                CreatedById = user.Id
            };

            _db.MealPlanSnapshots.Add(mealPlanSnapshot);
            await _db.SaveChangesAsync();

            var shoppingSnapshot = new ShoppingListSnapshot
            {
                MealPlanId = createdPlans.FirstOrDefault()?.Id,
                MealPlanSnapshotId = mealPlanSnapshot.Id,
                CreatedAt = DateTime.UtcNow,
                SourceType = "calendar-import",
                CreatedById = user.Id
            };

            var shoppingPayload = new ShoppingListSnapshotPayload
            {
                WeekStart = weekStartUtc,
                WeekEnd = weekEndUtc,
                Range = range,
                MealPlanIds = createdPlans.Select(p => p.Id).ToList(),
                Plans = createdPlans.Select(p => new ShoppingListPlanSummary
                {
                    Id = p.Id,
                    Name = p.Name,
                    Date = p.Date
                }).ToList(),
                ShoppingList = shoppingList,
                MealPlanSnapshotId = mealPlanSnapshot.Id
            };

            shoppingSnapshot.JsonData = System.Text.Json.JsonSerializer.Serialize(shoppingPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            _db.ShoppingListSnapshots.Add(shoppingSnapshot);
            await _db.SaveChangesAsync();

            mealPlanSnapshot.ShoppingListSnapshotId = shoppingSnapshot.Id;

            var mealPlanPayload = new MealPlanSnapshotPayload
            {
                WeekStart = weekStartUtc,
                WeekEnd = weekEndUtc,
                Range = range,
                Plans = new List<MealPlanSnapshotPlan>(),
                ShoppingListSnapshotId = shoppingSnapshot.Id
            };

            var recipeLookup = await _db.Recipes.AsNoTracking()
                .Select(r => new { r.Id, r.Title })
                .ToDictionaryAsync(r => r.Id, r => r.Title);

            foreach (var plan in createdPlans)
            {
                var planDto = planDtos.First(p => p.Id == plan.Id);
                foreach (var meal in plan.Meals)
                {
                    var autoHandled = MealUtilities.ShouldAutoHandleMeal(meal);
                    var mealDto = new CalendarImportMealDto
                    {
                        MealId = meal.Id,
                        MealType = meal.MealType,
                        RecipeId = meal.RecipeId,
                        RecipeName = meal.RecipeId.HasValue && recipeLookup.TryGetValue(meal.RecipeId.Value, out var title) ? title : null,
                        MissingRecipe = !meal.RecipeId.HasValue && !autoHandled,
                        AutoHandled = autoHandled,
                        FreeText = meal.FreeText,
                        IsSelected = meal.IsSelected
                    };

                    planDto.Meals.Add(mealDto);
                }

                mealPlanPayload.Plans.Add(new MealPlanSnapshotPlan
                {
                    Id = plan.Id,
                    Name = plan.Name,
                    Date = plan.Date,
                    Meals = planDto.Meals.Select(m => new MealPlanSnapshotMeal
                    {
                        MealId = m.MealId,
                        MealType = m.MealType,
                        RecipeName = m.RecipeName,
                        MissingRecipe = m.MissingRecipe,
                        AutoHandled = m.AutoHandled,
                        FreeText = m.FreeText,
                        IsSelected = m.IsSelected
                    }).ToList()
                });

                var missingMeals = plan.Meals
                    .Where(meal => !meal.RecipeId.HasValue && !MealUtilities.ShouldAutoHandleMeal(meal))
                    .Select(meal => new CalendarImportMissingMealDto
                    {
                        MealPlanId = plan.Id,
                        MealPlanName = plan.Name,
                        MealType = meal.MealType,
                        Name = meal.FreeText ?? "Meal"
                    });

                missingMealsDtos.AddRange(missingMeals);
            }

            mealPlanSnapshot.JsonData = System.Text.Json.JsonSerializer.Serialize(mealPlanPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            _db.MealPlanSnapshots.Update(mealPlanSnapshot);
            await _db.SaveChangesAsync();

            return new CalendarImportResult
            {
                Range = range,
                WeekStart = weekStartUtc,
                WeekEnd = weekEndUtc,
                TotalPlans = createdPlans.Count,
                ShoppingListId = shoppingSnapshot.Id,
                MealPlanSnapshotId = mealPlanSnapshot.Id,
                Plans = planDtos,
                MissingMeals = missingMealsDtos,
                ShoppingList = shoppingList
            };
        }

        private static string NormalizeUrl(string url)
        {
            if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + url.Substring("webcal://".Length);
            }
            return url;
        }

    }
}
