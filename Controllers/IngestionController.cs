using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;
using RecipeApp.Services;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using System.ClientModel;
using Microsoft.AspNetCore.Hosting;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IngestionController : ControllerBase
    {
        private readonly AppDb _db;
        private readonly string _apiKey;
        private readonly IUserContext _userContext;
        private readonly LlmNutritionEstimator _nutritionEstimator;
        private readonly IWebHostEnvironment _env;

        public IngestionController(AppDb db, IConfiguration config, IUserContext userContext, LlmNutritionEstimator nutritionEstimator, IWebHostEnvironment env)
        {
            _db = db;
            _apiKey = (config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey missing")).Trim();
            _userContext = userContext;
            _nutritionEstimator = nutritionEstimator;
            _env = env;
        }

        private static (decimal? amount, string? unit) ParseQuantity(string? quantity)
        {
            if (string.IsNullOrWhiteSpace(quantity)) return (null, null);
            var parts = quantity.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return (null, null);

            if (decimal.TryParse(parts[0], out var amount))
            {
                var unit = parts.Length > 1 ? parts[1] : null;
                return (amount, unit);
            }

            return (null, quantity);
        }

 // ✅ SINGLE IMAGE UPLOAD
          [HttpPost("upload")]
            [Consumes("multipart/form-data")]
            public async Task<IActionResult> Upload([FromForm] UploadRecipeImageDto dto)
            {
            var file = dto.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded.");


            var tempPath = Path.GetTempFileName();
            await using (var stream = System.IO.File.Create(tempPath))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
                var chatClient = new ChatClient("gpt-4o-mini", new ApiKeyCredential(_apiKey));

                var response = await chatClient.CompleteChatAsync(new[]
                {
                    ChatMessage.CreateUserMessage(new ChatMessageContentPart[]
                    {
                        ChatMessageContentPart.CreateTextPart(
                            "Extract recipe details as JSON with macros if present. " +
                            "Schema: { title: string, servings: number, calories?: number, protein?: number, carbs?: number, fat?: number, " +
                            "ingredients: [ { name: string, quantity: string } ] }. " +
                            "Calories/protein/carbs/fat should be per serving if shown. Return ONLY JSON."
                        ),
                        ChatMessageContentPart.CreateImagePart(new BinaryData(bytes), file.ContentType)
                    })
                });

                var content = response.Value.Content[0].Text ?? "";
                var json = content.Replace("```json", "").Replace("```", "");

                var extracted = JsonSerializer.Deserialize<ExtractRecipeDto>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (extracted == null)
                    return BadRequest("Failed to parse recipe content.");

                var currentUser = await _userContext.GetCurrentUserAsync();
                var imageUrl = SaveRecipeImage(file, tempPath);

                var recipe = new Recipe
                {
                    Id = Guid.NewGuid(),
                    Title = extracted.Title,
                    Servings = extracted.Servings,
                    RecipeIngredients = new List<RecipeIngredient>(),
                    OwnerId = currentUser.Id,
                    IsGlobal = false,
                    AssignedToId = currentUser.Role == "Client" ? currentUser.Id : null,
                    ImageUrl = imageUrl
                };

                foreach (var i in extracted.Ingredients)
                {
                    (decimal? amount, string? unitStr) = ParseQuantity(i.Quantity);
                    Unit? unit = null;

                    if (!string.IsNullOrWhiteSpace(unitStr))
                        unit = await _db.Units.FirstOrDefaultAsync(u => u.Code == unitStr);

                    var ingredient = await _db.Ingredients
                        .FirstOrDefaultAsync(x => x.Name.ToLower() == i.Name.ToLower());

                    if (ingredient == null)
                    {
                        ingredient = new Ingredient
                        {
                            Id = Guid.NewGuid(),
                            Name = i.Name
                        };
                        _db.Ingredients.Add(ingredient);
                    }

                    recipe.RecipeIngredients.Add(new RecipeIngredient
                    {
                        Id = Guid.NewGuid(),
                        RecipeId = recipe.Id,
                        Ingredient = ingredient,
                        Amount = amount,
                        Unit = unit,
                        Notes = i.Quantity
                    });
                }

                // Apply macros from card if present
                if (extracted.Calories.HasValue) recipe.Calories = extracted.Calories.Value;
                if (extracted.Protein.HasValue) recipe.Protein = extracted.Protein.Value;
                if (extracted.Carbs.HasValue) recipe.Carbs = extracted.Carbs.Value;
                if (extracted.Fat.HasValue) recipe.Fat = extracted.Fat.Value;
                recipe.MacrosEstimated = extracted.Calories.HasValue || extracted.Protein.HasValue ||
                                         extracted.Carbs.HasValue || extracted.Fat.HasValue;

                // If macros missing, estimate from ingredients
                if (!HasMacroValues(recipe))
                {
                    var ingredientLines = recipe.RecipeIngredients.Select(ri =>
                    {
                        var parts = new List<string>();
                        if (ri.Amount.HasValue) parts.Add(ri.Amount.Value.ToString());
                        if (!string.IsNullOrWhiteSpace(ri.Unit?.Code)) parts.Add(ri.Unit!.Code!);
                        parts.Add(ri.Ingredient.Name);
                        return string.Join(" ", parts);
                    });

                    var estimate = await _nutritionEstimator.EstimateRecipeAsync(
                        recipe.Title,
                        ingredientLines,
                        recipe.Servings,
                        HttpContext.RequestAborted);

                    if (estimate != null)
                    {
                        recipe.Calories = estimate.Calories;
                        recipe.Protein = estimate.Protein;
                        recipe.Carbs = estimate.Carbs;
                        recipe.Fat = estimate.Fat;
                        recipe.MacrosEstimated = true;
                    }
                }

                _db.Recipes.Add(recipe);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    recipe.Title,
                    recipe.Servings,
                    Ingredients = recipe.RecipeIngredients.Select(ri => new
                    {
                        ri.Ingredient.Name,
                        ri.Amount,
                        Unit = ri.Unit?.Code,
                        ri.Notes
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
            finally
            {
                try { System.IO.File.Delete(tempPath); } catch { }
            }
        }

        // ✅ MULTI IMAGE UPLOAD (Batch)
        [HttpPost("upload/batch")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadBatch([FromForm] UploadRecipeImageDto dto)
        {
            var files = dto.Files;
            if (files == null || files.Count == 0)
                return BadRequest("No files uploaded.");

            var chatClient = new ChatClient("gpt-4o-mini", new ApiKeyCredential(_apiKey));
            var currentUser = await _userContext.GetCurrentUserAsync();
            var createdRecipes = new List<object>();

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var tempPath = Path.GetTempFileName();
                await using (var stream = System.IO.File.Create(tempPath))
                {
                    await file.CopyToAsync(stream);
                }

                try
                {
                    var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
                    var imageUrl = SaveRecipeImage(file, tempPath);

                    var response = await chatClient.CompleteChatAsync(new[]
                    {
                        ChatMessage.CreateUserMessage(new ChatMessageContentPart[]
                        {
                            ChatMessageContentPart.CreateTextPart(
                                "Extract recipe details as JSON with macros if present. " +
                                "Schema: { title: string, servings: number, calories?: number, protein?: number, carbs?: number, fat?: number, " +
                                "ingredients: [ { name: string, quantity: string } ] }. " +
                                "Calories/protein/carbs/fat should be per serving if shown. Return ONLY JSON."
                            ),
                            ChatMessageContentPart.CreateImagePart(new BinaryData(bytes), file.ContentType)
                        })
                    });

                    var content = response.Value.Content[0].Text ?? "";
                    var json = content.Replace("```json", "").Replace("```", "");

                    var extracted = JsonSerializer.Deserialize<ExtractRecipeDto>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (extracted == null) continue;

                    var recipe = new Recipe
                    {
                        Id = Guid.NewGuid(),
                        Title = extracted.Title,
                        Servings = extracted.Servings,
                        RecipeIngredients = new List<RecipeIngredient>(),
                        ImageUrl = imageUrl
                    };
                    recipe.OwnerId = currentUser.Id;
                    recipe.IsGlobal = false;
                    recipe.AssignedToId = currentUser.Role == "Client" ? currentUser.Id : null;

                    foreach (var i in extracted.Ingredients)
                    {
                        (decimal? amount, string? unitStr) = ParseQuantity(i.Quantity);
                        Unit? unit = null;

                        if (!string.IsNullOrWhiteSpace(unitStr))
                            unit = await _db.Units.FirstOrDefaultAsync(u => u.Code == unitStr);

                        var ingredient = await _db.Ingredients
                            .FirstOrDefaultAsync(x => x.Name.ToLower() == i.Name.ToLower());

                        if (ingredient == null)
                        {
                            ingredient = new Ingredient
                            {
                                Id = Guid.NewGuid(),
                                Name = i.Name
                            };
                            _db.Ingredients.Add(ingredient);
                        }

                        recipe.RecipeIngredients.Add(new RecipeIngredient
                        {
                            Id = Guid.NewGuid(),
                            RecipeId = recipe.Id,
                            Ingredient = ingredient,
                            Amount = amount,
                            Unit = unit,
                            Notes = i.Quantity
                        });
                    }

                    if (extracted.Calories.HasValue) recipe.Calories = extracted.Calories.Value;
                    if (extracted.Protein.HasValue) recipe.Protein = extracted.Protein.Value;
                    if (extracted.Carbs.HasValue) recipe.Carbs = extracted.Carbs.Value;
                    if (extracted.Fat.HasValue) recipe.Fat = extracted.Fat.Value;
                    recipe.MacrosEstimated = extracted.Calories.HasValue || extracted.Protein.HasValue ||
                                             extracted.Carbs.HasValue || extracted.Fat.HasValue;

                    if (!HasMacroValues(recipe))
                    {
                        var ingredientLines = recipe.RecipeIngredients.Select(ri =>
                        {
                            var parts = new List<string>();
                            if (ri.Amount.HasValue) parts.Add(ri.Amount.Value.ToString());
                            if (!string.IsNullOrWhiteSpace(ri.Unit?.Code)) parts.Add(ri.Unit!.Code!);
                            parts.Add(ri.Ingredient.Name);
                            return string.Join(" ", parts);
                        });

                        var estimate = await _nutritionEstimator.EstimateRecipeAsync(
                            recipe.Title,
                            ingredientLines,
                            recipe.Servings,
                            HttpContext.RequestAborted);

                        if (estimate != null)
                        {
                            recipe.Calories = estimate.Calories;
                            recipe.Protein = estimate.Protein;
                            recipe.Carbs = estimate.Carbs;
                            recipe.Fat = estimate.Fat;
                            recipe.MacrosEstimated = true;
                        }
                    }

                    _db.Recipes.Add(recipe);

                    createdRecipes.Add(new
                    {
                        recipe.Title,
                        recipe.Servings,
                        Ingredients = recipe.RecipeIngredients.Select(ri => new
                        {
                            ri.Ingredient.Name,
                            ri.Amount,
                            Unit = ri.Unit?.Code,
                            ri.Notes
                        })
                    });
                }
                catch (Exception ex)
                {
                    createdRecipes.Add(new { error = $"Failed to parse {file.FileName}", details = ex.Message });
                }
                finally
                {
                    try { System.IO.File.Delete(tempPath); } catch { }
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { recipes = createdRecipes });
        }

        private static bool HasMacroValues(Recipe recipe)
        {
            return (recipe.Calories > 0) || (recipe.Protein > 0) || (recipe.Carbs > 0) || (recipe.Fat > 0);
        }

        private string SaveRecipeImage(IFormFile file, string tempPath)
        {
            var uploadsRoot = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "recipes");
            Directory.CreateDirectory(uploadsRoot);

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".jpg";
            }

            var fileName = $"{Guid.NewGuid()}{ext}";
            var destPath = Path.Combine(uploadsRoot, fileName);
            System.IO.File.Copy(tempPath, destPath, true);

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/uploads/recipes/{fileName}";
        }
    }
}
