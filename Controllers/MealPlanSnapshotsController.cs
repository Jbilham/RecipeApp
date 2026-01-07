using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Helpers;
using RecipeApp.Models;
using RecipeApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Ical.Net;
using Ical.Net.Serialization;
using Microsoft.AspNetCore.Authorization;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/mealplans")]
    public class MealPlanSnapshotsController : ControllerBase
    {
        private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        private readonly AppDb _db;
        private readonly ShoppingListBuilder _shoppingListBuilder;
        private readonly IUserContext _userContext;
        private readonly MealPlanNutritionService _nutritionService;

        public MealPlanSnapshotsController(AppDb db, ShoppingListBuilder shoppingListBuilder, IUserContext userContext, MealPlanNutritionService nutritionService)
        {
            _db = db;
            _shoppingListBuilder = shoppingListBuilder;
            _userContext = userContext;
            _nutritionService = nutritionService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAllAsync()
        {
            var visibleUserIds = await _userContext.GetVisibleUserIdsAsync();

            var snapshots = await _db.MealPlanSnapshots
                .Where(s => !s.CreatedById.HasValue || visibleUserIds.Contains(s.CreatedById.Value))
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var list = snapshots.Select(snapshot =>
            {
                var payload = DeserializeSnapshot(snapshot.JsonData);
                return ToSummary(snapshot, payload);
            });

            return Ok(list);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<object>> GetByIdAsync(Guid id)
        {
            var visibleUserIds = await _userContext.GetVisibleUserIdsAsync();

            var snapshot = await _db.MealPlanSnapshots.FindAsync(id);
            if (snapshot == null)
                return NotFound();

            if (snapshot.CreatedById.HasValue && !visibleUserIds.Contains(snapshot.CreatedById.Value))
                return Forbid();

            var payload = DeserializeSnapshot(snapshot.JsonData);
            if (payload == null)
                return BadRequest("Snapshot is corrupted.");

            return Ok(ToDetail(snapshot, payload));
        }

        [HttpPatch("{id:guid}/selections")]
        public async Task<ActionResult<object>> UpdateSelectionsAsync(Guid id, [FromBody] UpdateMealSelectionsDto dto)
        {
            if (dto == null || dto.Meals == null || dto.Meals.Count == 0)
            {
                return BadRequest("At least one meal selection is required.");
            }

            var snapshot = await _db.MealPlanSnapshots
                .Include(s => s.ShoppingListSnapshot)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (snapshot == null)
            {
                return NotFound();
            }

            if (snapshot.CreatedById.HasValue)
            {
                var visibleUserIds = await _userContext.GetVisibleUserIdsAsync();
                if (!visibleUserIds.Contains(snapshot.CreatedById.Value))
                {
                    return Forbid();
                }
            }

            var payload = DeserializeSnapshot(snapshot.JsonData);
            if (payload == null)
            {
                return BadRequest("Snapshot is corrupted.");
            }

            var targetMealIds = dto.Meals.Select(m => m.MealId).Distinct().ToList();
            var meals = await _db.Meals
                .Include(m => m.MealPlan)
                .Where(m => targetMealIds.Contains(m.Id))
                .ToListAsync();

            if (meals.Count == 0)
            {
                return NotFound("No meals matched the provided identifiers.");
            }

            var planIdSet = payload.Plans.Select(p => p.Id).ToHashSet();

            foreach (var selection in dto.Meals)
            {
                var meal = meals.FirstOrDefault(m => m.Id == selection.MealId);
                if (meal == null)
                {
                    continue;
                }

                if (!planIdSet.Contains(meal.MealPlanId))
                {
                    return BadRequest("One or more meals do not belong to this meal plan snapshot.");
                }

                meal.IsSelected = selection.IsSelected;
            }

            var affectedPlans = meals
                .Select(m => m.MealPlan)
                .Where(p => p != null)
                .Distinct()!
                .ToList();

            foreach (var plan in affectedPlans)
            {
                if (plan == null) continue;
                plan.FreeItems = plan.Meals
                    .Where(m => m.IsSelected)
                    .SelectMany(m => m.ExtraItems)
                    .ToList();
            }

            await _db.SaveChangesAsync();

            var rebuildResult = await RebuildSnapshotAsync(snapshot, payload);

            snapshot.JsonData = JsonSerializer.Serialize(rebuildResult.MealPlanPayload, SnapshotJsonOptions);
            _db.MealPlanSnapshots.Update(snapshot);

            if (snapshot.ShoppingListSnapshotId.HasValue && rebuildResult.ShoppingPayload != null)
            {
                var shoppingSnapshot = await _db.ShoppingListSnapshots
                    .FirstOrDefaultAsync(s => s.Id == snapshot.ShoppingListSnapshotId.Value);
                if (shoppingSnapshot != null)
                {
                    shoppingSnapshot.JsonData = JsonSerializer.Serialize(rebuildResult.ShoppingPayload, SnapshotJsonOptions);
                    _db.ShoppingListSnapshots.Update(shoppingSnapshot);
                }
            }

            await _db.SaveChangesAsync();

            return Ok(ToDetail(snapshot, rebuildResult.MealPlanPayload));
        }

        private static object ToSummary(MealPlanSnapshot snapshot, MealPlanSnapshotPayload? payload)
        {
            var title = BuildTitle(payload);
            return new
            {
                id = snapshot.Id,
                title,
                weekStart = payload?.WeekStart,
                weekEnd = payload?.WeekEnd,
                createdAt = snapshot.CreatedAt,
                range = payload?.Range,
                planCount = payload?.Plans?.Count ?? 0,
                shoppingListSnapshotId = payload?.ShoppingListSnapshotId,
                weeklyNutritionTotals = payload?.WeeklyNutritionTotals
            };
        }

        private static object ToDetail(MealPlanSnapshot snapshot, MealPlanSnapshotPayload payload)
        {
            var title = BuildTitle(payload);
            return new
            {
                id = snapshot.Id,
                title,
                range = payload.Range,
                weekStart = payload.WeekStart,
                weekEnd = payload.WeekEnd,
                createdAt = snapshot.CreatedAt,
                weeklyNutritionTotals = payload.WeeklyNutritionTotals,
                plans = payload.Plans ?? new List<MealPlanSnapshotPlan>(),
                shoppingListSnapshotId = payload.ShoppingListSnapshotId
            };
        }

        // iCal export for a snapshot
        [HttpGet("{id:guid}/ics")]
        public async Task<IActionResult> ExportIcsAsync(Guid id)
        {
            var visibleUserIds = await _userContext.GetVisibleUserIdsAsync();
            var snapshot = await _db.MealPlanSnapshots.FindAsync(id);
            if (snapshot == null)
                return NotFound();

            if (snapshot.CreatedById.HasValue && !visibleUserIds.Contains(snapshot.CreatedById.Value))
                return Forbid();

            var payload = DeserializeSnapshot(snapshot.JsonData);
            if (payload == null || payload.Plans == null || payload.Plans.Count == 0)
                return BadRequest("Snapshot is empty.");

            // Default meal times
            var today = DateTime.UtcNow.Date;
            var diffToMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var startOfThisWeek = today.AddDays(-diffToMonday);
            var endOfNextWeek = startOfThisWeek.AddDays(14);

            var defaults = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                { "breakfast", new TimeSpan(7, 30, 0) },
                { "mid-morning", new TimeSpan(10, 30, 0) },
                { "snack", new TimeSpan(10, 30, 0) },
                { "lunch", new TimeSpan(12, 30, 0) },
                { "mid-afternoon", new TimeSpan(15, 0, 0) },
                { "afternoon snack", new TimeSpan(15, 0, 0) },
                { "dinner", new TimeSpan(19, 0, 0) },
                { "evening", new TimeSpan(19, 0, 0) }
            };

            var calendar = new Ical.Net.Calendar();
            foreach (var plan in payload.Plans
                         .Where(p => p.Date.HasValue &&
                                     p.Date.Value.Date >= startOfThisWeek &&
                                     p.Date.Value.Date < endOfNextWeek)
                         .OrderBy(p => p.Date))
            {
                if (plan.Meals == null || plan.Meals.Count == 0 || plan.Date == null)
                    continue;

                foreach (var meal in plan.Meals.Where(m => m.IsSelected != false))
                {
                    var time = defaults
                        .Where(kvp => meal.MealType != null && meal.MealType.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        .Select(kvp => kvp.Value)
                        .FirstOrDefault(new TimeSpan(12, 0, 0));

                    var start = DateTime.SpecifyKind(plan.Date.Value.Date + time, DateTimeKind.Utc);
                    var summary = BuildEventSummary(meal);
                    var desc = BuildEventDescription(meal);
                    var ev = new Ical.Net.CalendarComponents.CalendarEvent
                    {
                        Summary = summary,
                        Description = desc,
                        Start = new Ical.Net.DataTypes.CalDateTime(start),
                        End = new Ical.Net.DataTypes.CalDateTime(start.AddMinutes(45))
                    };
                    calendar.Events.Add(ev);
                }
            }

            var serializer = new Ical.Net.Serialization.CalendarSerializer();
            var ics = serializer.SerializeToString(calendar) ?? string.Empty;
            var bytes = System.Text.Encoding.UTF8.GetBytes(ics);
            return File(bytes, "text/calendar", "mealplan.ics");
        }

        // Stable per-user calendar URL: latest snapshot for current user, limited to this and next week
        [HttpGet("ics/me")]
        public async Task<IActionResult> ExportMyIcsAsync()
        {
            var currentUser = await _userContext.GetCurrentUserAsync();
            var snapshot = await _db.MealPlanSnapshots
                .Where(s => s.CreatedById == currentUser.Id)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (snapshot == null)
                return NotFound("No meal plan snapshots for this user.");

            // reuse existing export logic
            return await ExportIcsAsync(snapshot.Id);
        }

        // Return the public ICS URL for the current user (create token if missing)
        [HttpGet("ics/url")]
        [Authorize]
        public async Task<IActionResult> GetMyCalendarUrlAsync()
        {
            var user = await _userContext.GetCurrentUserAsync();
            if (string.IsNullOrWhiteSpace(user.PublicCalendarToken))
            {
                user.PublicCalendarToken = Guid.NewGuid().ToString("N");
                _db.Users.Update(user);
                await _db.SaveChangesAsync();
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var url = $"{baseUrl}/api/mealplans/ics/public/{user.PublicCalendarToken}";
            return Ok(new { url });
        }

        private static string BuildTitle(MealPlanSnapshotPayload? payload)
        {
            if (payload != null && payload.WeekStart != default)
            {
                return $"Week commencing Monday {payload.WeekStart:dd MMM yyyy}";
            }

            return "Meal Plan";
        }

        [AllowAnonymous]
        [HttpGet("ics/public/{token}")]
        public async Task<IActionResult> ExportPublicIcsAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("Token is required.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicCalendarToken == token);
            if (user == null)
                return NotFound();

            var snapshot = await _db.MealPlanSnapshots
                .Where(s => s.CreatedById == user.Id)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (snapshot == null)
                return NotFound("No meal plan snapshots for this user.");

            // Temporarily bypass visibleUserIds since this is a public feed per user
            var payload = DeserializeSnapshot(snapshot.JsonData);
            if (payload == null || payload.Plans == null || payload.Plans.Count == 0)
                return BadRequest("Snapshot is empty.");

            // Default meal times
            var today = DateTime.UtcNow.Date;
            var diffToMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var startOfThisWeek = today.AddDays(-diffToMonday);
            var endOfNextWeek = startOfThisWeek.AddDays(14);

            var defaults = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                { "breakfast", new TimeSpan(7, 30, 0) },
                { "mid-morning", new TimeSpan(10, 30, 0) },
                { "snack", new TimeSpan(10, 30, 0) },
                { "lunch", new TimeSpan(12, 30, 0) },
                { "mid-afternoon", new TimeSpan(15, 0, 0) },
                { "afternoon snack", new TimeSpan(15, 0, 0) },
                { "dinner", new TimeSpan(19, 0, 0) },
                { "evening", new TimeSpan(19, 0, 0) }
            };

            var calendar = new Ical.Net.Calendar();
            foreach (var plan in payload.Plans
                         .Where(p => p.Date.HasValue &&
                                     p.Date.Value.Date >= startOfThisWeek &&
                                     p.Date.Value.Date < endOfNextWeek)
                         .OrderBy(p => p.Date))
            {
                if (plan.Meals == null || plan.Meals.Count == 0 || plan.Date == null)
                    continue;

                foreach (var meal in plan.Meals.Where(m => m.IsSelected != false))
                {
                    var time = defaults
                        .Where(kvp => meal.MealType != null && meal.MealType.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        .Select(kvp => kvp.Value)
                        .FirstOrDefault(new TimeSpan(12, 0, 0));

                    var start = DateTime.SpecifyKind(plan.Date.Value.Date + time, DateTimeKind.Utc);
                    var summary = BuildEventSummary(meal);
                    var desc = BuildEventDescription(meal);
                    var ev = new Ical.Net.CalendarComponents.CalendarEvent
                    {
                        Summary = summary,
                        Description = desc,
                        Start = new Ical.Net.DataTypes.CalDateTime(start),
                        End = new Ical.Net.DataTypes.CalDateTime(start.AddMinutes(45))
                    };
                    calendar.Events.Add(ev);
                }
            }

            var serializer = new Ical.Net.Serialization.CalendarSerializer();
            var ics = serializer.SerializeToString(calendar) ?? string.Empty;
            var bytes = System.Text.Encoding.UTF8.GetBytes(ics);
            return File(bytes, "text/calendar", "mealplan.ics");
        }

        private static MealPlanSnapshotPayload? DeserializeSnapshot(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<MealPlanSnapshotPayload>(json, SnapshotJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static string BuildEventSummary(MealPlanSnapshotMeal meal)
        {
            var name = string.IsNullOrWhiteSpace(meal.RecipeName)
                ? (meal.FreeText ?? "Meal")
                : meal.RecipeName;
            var label = HasNutrition(meal.Nutrition)
                ? $" ({FormatKcal(meal.Nutrition!.Calories)} | {FormatGrams(meal.Nutrition.Protein)}P/{FormatGrams(meal.Nutrition.Carbs)}C/{FormatGrams(meal.Nutrition.Fat)}F)"
                : string.Empty;
            return $"{meal.MealType}: {name}{label}";
        }

        private static string BuildEventDescription(MealPlanSnapshotMeal meal)
        {
            if (!HasNutrition(meal.Nutrition))
                return meal.FreeText ?? string.Empty;

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(meal.FreeText))
                parts.Add(meal.FreeText);
            parts.Add($"Calories: {FormatKcal(meal.Nutrition!.Calories)}");
            parts.Add($"Protein: {FormatGrams(meal.Nutrition.Protein)} g");
            parts.Add($"Carbs: {FormatGrams(meal.Nutrition.Carbs)} g");
            parts.Add($"Fat: {FormatGrams(meal.Nutrition.Fat)} g");
            return string.Join("\n", parts);
        }

        private static bool HasNutrition(MealNutritionDto? n)
        {
            if (n == null) return false;
            return n.Calories > 0 || n.Protein > 0 || n.Carbs > 0 || n.Fat > 0;
        }

        private static string FormatKcal(decimal value) =>
            Math.Round(value).ToString("0") + " kcal";

        private static string FormatGrams(decimal value) =>
            Math.Round(value).ToString("0");

        private async Task<RebuildResult> RebuildSnapshotAsync(MealPlanSnapshot snapshot, MealPlanSnapshotPayload payload)
        {
            var planIds = payload.Plans.Select(p => p.Id).ToList();

            var plans = await _db.MealPlans
                .Include(p => p.Meals)
                    .ThenInclude(m => m.Recipe)
                .Where(p => planIds.Contains(p.Id))
                .ToListAsync();

            var nutritionResult = await _nutritionService.EnsureNutritionAsync(plans);

            var planOrder = payload.Plans
                .Select((plan, index) => new { plan.Id, index })
                .ToDictionary(x => x.Id, x => x.index);

            var updatedPlans = new List<MealPlanSnapshotPlan>();
            var selectedMeals = new List<Meal>();

            foreach (var plan in plans)
            {
                nutritionResult.PlanTotals.TryGetValue(plan.Id, out var totals);

                var planPayload = new MealPlanSnapshotPlan
                {
                    Id = plan.Id,
                    Name = plan.Name,
                    Date = plan.Date,
                    NutritionTotals = totals,
                    Meals = plan.Meals
                        .OrderBy(m => m.MealType)
                        .ThenBy(m => m.Id)
                        .Select(meal =>
                        {
                            var autoHandled = MealUtilities.ShouldAutoHandleMeal(meal);
                            return new MealPlanSnapshotMeal
                            {
                                MealId = meal.Id,
                                MealType = meal.MealType,
                                RecipeName = meal.Recipe?.Title,
                                MissingRecipe = !meal.RecipeId.HasValue && !autoHandled,
                                AutoHandled = autoHandled,
                                FreeText = meal.FreeText,
                                IsSelected = meal.IsSelected,
                                Nutrition = ToMealNutritionDto(meal)
                            };
                        })
                        .ToList()
                };

                updatedPlans.Add(planPayload);
                selectedMeals.AddRange(plan.Meals.Where(m => m.IsSelected));
            }

            var missingPlans = payload.Plans
                .Where(p => updatedPlans.All(up => up.Id != p.Id))
                .ToList();
            updatedPlans.AddRange(missingPlans);

            var orderedPlans = updatedPlans
                .OrderBy(p => planOrder.TryGetValue(p.Id, out var idx) ? idx : int.MaxValue)
                .ToList();

            var newPayload = new MealPlanSnapshotPayload
            {
                WeekStart = payload.WeekStart,
                WeekEnd = payload.WeekEnd,
                Range = payload.Range,
                Plans = orderedPlans,
                ShoppingListSnapshotId = payload.ShoppingListSnapshotId,
                WeeklyNutritionTotals = nutritionResult.WeeklyTotals
            };

            var recipeIds = selectedMeals
                .Where(m => m.RecipeId.HasValue)
                .Select(m => m.RecipeId!.Value)
                .ToList();

            var extraItems = selectedMeals
                .SelectMany(m => m.ExtraItems)
                .ToList();

            var shoppingList = await _shoppingListBuilder.BuildAsync(recipeIds, extraItems);

            var shoppingPayload = new ShoppingListSnapshotPayload
            {
                WeekStart = newPayload.WeekStart,
                WeekEnd = newPayload.WeekEnd,
                Range = newPayload.Range,
                MealPlanIds = orderedPlans.Select(p => p.Id).ToList(),
                Plans = orderedPlans.Select(p => new ShoppingListPlanSummary
                {
                    Id = p.Id,
                    Name = p.Name,
                    Date = p.Date
                }).ToList(),
                ShoppingList = shoppingList,
                MealPlanSnapshotId = snapshot.Id
            };

            return new RebuildResult(newPayload, shoppingPayload);
        }

        private static MealNutritionDto? ToMealNutritionDto(Meal meal)
        {
            if (meal.Calories == null && meal.Protein == null && meal.Carbs == null && meal.Fat == null)
                return null;

            return new MealNutritionDto
            {
                Calories = meal.Calories ?? 0,
                Protein = meal.Protein ?? 0,
                Carbs = meal.Carbs ?? 0,
                Fat = meal.Fat ?? 0,
                Source = meal.NutritionSource,
                Estimated = meal.NutritionEstimated
            };
        }

        private sealed record RebuildResult(MealPlanSnapshotPayload MealPlanPayload, ShoppingListSnapshotPayload ShoppingPayload);
    }
}
