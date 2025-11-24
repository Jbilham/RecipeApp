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
            var snapshots = await _db.MealPlanSnapshots
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
            var snapshot = await _db.MealPlanSnapshots.FindAsync(id);
            if (snapshot == null)
                return NotFound();

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

        private static string BuildTitle(MealPlanSnapshotPayload? payload)
        {
            if (payload != null && payload.WeekStart != default)
            {
                return $"Week commencing Monday {payload.WeekStart:dd MMM yyyy}";
            }

            return "Meal Plan";
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
