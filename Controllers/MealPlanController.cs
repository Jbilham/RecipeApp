using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;
using RecipeApp.Services;

namespace RecipeApp.Controllers;

/// <summary>
/// Handles manual meal-plan creation and shopping-list generation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MealPlanController : ControllerBase
{
    private readonly AppDb _db;
    private readonly LlmMealPlanParser _llm;
    private readonly MealPlanAssembler _mealPlanAssembler;
    private readonly ShoppingListBuilder _shoppingListBuilder;

    public MealPlanController(
        AppDb db,
        LlmMealPlanParser llm,
        MealPlanAssembler mealPlanAssembler,
        ShoppingListBuilder shoppingListBuilder)
    {
        _db = db;
        _llm = llm;
        _mealPlanAssembler = mealPlanAssembler;
        _shoppingListBuilder = shoppingListBuilder;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<MealPlanDto>> CreateMealPlan([FromForm] CreateMealPlanDto dto)
    {
        Console.WriteLine("🔹 Creating new meal plan");

        if (dto == null) return BadRequest("Invalid meal plan.");

        var parsed = await _llm.ParseAsync(dto.Text);
        if (parsed == null) return BadRequest("Failed to parse meal plan text.");

        var plans = await _mealPlanAssembler.BuildWeeklyPlansAsync(new[] { parsed }, DateTime.Now);
        var plan = plans.First();
        _db.MealPlans.Add(plan);
        await _db.SaveChangesAsync();

        var recipeIds = plan.Meals
            .Where(m => m.RecipeId.HasValue)
            .Select(m => m.RecipeId!.Value)
            .ToList();
        var freeItems = plan.Meals
            .Where(m => !string.IsNullOrWhiteSpace(m.FreeText))
            .Select(m => m.FreeText!)
            .ToList();

        var shoppingList = await _shoppingListBuilder.BuildAsync(recipeIds, freeItems);

        Console.WriteLine("✅ Meal plan created successfully");
        return Ok(new
        {
            Plan = plan,
            ShoppingList = shoppingList
        });
    }

    // existing GET endpoints for weekly plans etc. can remain unchanged
}
