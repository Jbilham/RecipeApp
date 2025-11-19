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

        // Otherwise, create from structured meals provided in DTO
        Console.WriteLine("   • Creating plan from structured DTO");

        var meals = dto.Meals.Select(m => new Meal
        {
            Id = Guid.NewGuid(),
            MealType = m.MealType,
            RecipeId = m.RecipeId,
            FreeText = m.FreeText
        }).ToList();

        var planFromDto = new MealPlan
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Date = DateTime.SpecifyKind(dto.Date ?? DateTime.Now, DateTimeKind.Unspecified),
            Meals = meals
        };

        _db.MealPlans.Add(planFromDto);
        await _db.SaveChangesAsync();

        var recipeIdsDto = meals
            .Where(m => m.RecipeId.HasValue)
            .Select(m => m.RecipeId!.Value)
            .ToList();

        var freeItemsDto = meals
            .Where(m => !string.IsNullOrWhiteSpace(m.FreeText))
            .Select(m => m.FreeText!)
            .ToList();

        var shoppingListDto = await _shoppingListBuilder.BuildAsync(recipeIdsDto, freeItemsDto);

        Console.WriteLine("✅ Meal plan (DTO) created successfully");
        return Ok(new
        {
            Plan = planFromDto,
            ShoppingList = shoppingListDto
        });
    }
}