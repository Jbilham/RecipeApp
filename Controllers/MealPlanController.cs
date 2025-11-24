using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;
using RecipeApp.Services;

namespace RecipeApp.Controllers;

/// <summary>
/// Handles creation and retrieval of meal plans,
/// using shared MealPlanAssembler and ShoppingListBuilder services.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MealPlanController : ControllerBase
{
    private readonly AppDb _db;
    private readonly LlmMealPlanParser _llm;
    private readonly MealPlanAssembler _mealPlanAssembler;
    private readonly ShoppingListBuilder _shoppingListBuilder;
    private readonly IUserContext _userContext;
    private readonly MealPlanNutritionService _nutritionService;

    public MealPlanController(
        AppDb db,
        LlmMealPlanParser llm,
        MealPlanAssembler mealPlanAssembler,
        ShoppingListBuilder shoppingListBuilder,
        IUserContext userContext,
        MealPlanNutritionService nutritionService)
    {
        _db = db;
        _llm = llm;
        _mealPlanAssembler = mealPlanAssembler;
        _shoppingListBuilder = shoppingListBuilder;
        _userContext = userContext;
        _nutritionService = nutritionService;
    }

    /// <summary>
    /// Creates a meal plan either from pasted free text or a structured DTO.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> CreateMealPlan([FromForm] CreateMealPlanDto dto)
    {
        Console.WriteLine("🔹 Creating new meal plan via API…");

        if (dto == null) return BadRequest("Invalid meal plan payload.");

        // If user pasted a full text plan, parse it via LLM
        if (!string.IsNullOrWhiteSpace(dto.FreeText))
        {
            Console.WriteLine("   • Parsing FreeText plan via LLM");
            var parsed = await _llm.ParseAsync(dto.FreeText);
            if (parsed == null)
                return BadRequest("Failed to parse meal plan text.");

            var plan = await _mealPlanAssembler.CreateMealPlanFromParsedAsync(
                parsed,
                dto.Date ?? DateTime.Now,
                dto.Name ?? "Imported Plan"
            );

            var currentUser = await _userContext.GetCurrentUserAsync();
            plan.CreatedById = currentUser.Id;
            plan.AssignedToId ??= currentUser.Id;

            var selectedMeals = plan.Meals.Where(m => m.IsSelected).ToList();

            var recipeIds = selectedMeals
                .Where(m => m.RecipeId.HasValue)
                .Select(m => m.RecipeId!.Value)
                .ToList();

            var freeItems = selectedMeals
                .SelectMany(m => m.ExtraItems)
                .ToList();

            plan.FreeItems = freeItems;

            var nutrition = await _nutritionService.EnsureNutritionAsync(new[] { plan });
            await _db.SaveChangesAsync();

            var shoppingList = await _shoppingListBuilder.BuildAsync(recipeIds, freeItems);

            Console.WriteLine("✅ Meal plan created successfully");
            return Ok(new
            {
                Plan = plan,
                ShoppingList = shoppingList,
                Nutrition = BuildNutritionSummary(plan, nutrition)
            });
        }

        // Otherwise, create from structured meals provided in DTO
        Console.WriteLine("   • Creating plan from structured DTO");

        var meals = dto.Meals.Select(m => new Meal
        {
            Id = Guid.NewGuid(),
            MealType = m.MealType,
            RecipeId = m.RecipeId,
            FreeText = m.FreeText,
            ExtraItems = string.IsNullOrWhiteSpace(m.FreeText)
                ? new List<string>()
                : new List<string> { m.FreeText! },
            IsSelected = true
        }).ToList();

        var currentUserForDto = await _userContext.GetCurrentUserAsync();

        var planFromDto = new MealPlan
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Date = DateTime.SpecifyKind(dto.Date ?? DateTime.Now, DateTimeKind.Unspecified),
            Meals = meals,
            CreatedById = currentUserForDto.Id,
            AssignedToId = dto.AssignedUserId ?? currentUserForDto.Id
        };

        var selectedMealsDto = meals.Where(m => m.IsSelected).ToList();

        var recipeIdsDto = selectedMealsDto
            .Where(m => m.RecipeId.HasValue)
            .Select(m => m.RecipeId!.Value)
            .ToList();

        var freeItemsDto = selectedMealsDto
            .SelectMany(m => m.ExtraItems)
            .ToList();

        planFromDto.FreeItems = freeItemsDto;

        _db.MealPlans.Add(planFromDto);
        var dtoNutrition = await _nutritionService.EnsureNutritionAsync(new[] { planFromDto });
        await _db.SaveChangesAsync();

        var shoppingListDto = await _shoppingListBuilder.BuildAsync(recipeIdsDto, freeItemsDto);

        Console.WriteLine("✅ Meal plan (DTO) created successfully");
        return Ok(new
        {
            Plan = planFromDto,
            ShoppingList = shoppingListDto,
            Nutrition = BuildNutritionSummary(planFromDto, dtoNutrition)
        });
    }

    private static MealPlanNutritionSummaryDto BuildNutritionSummary(MealPlan plan, NutritionComputationResult result)
    {
        var summary = new MealPlanNutritionSummaryDto
        {
            WeeklyTotals = result.WeeklyTotals
        };

        if (!result.PlanTotals.TryGetValue(plan.Id, out var totals))
        {
            totals = new NutritionBreakdownDto();
        }

        summary.Plans.Add(new PlanNutritionSummaryDto
        {
            MealPlanId = plan.Id,
            Name = plan.Name,
            Date = plan.Date,
            Totals = totals
        });

        return summary;
    }
}
