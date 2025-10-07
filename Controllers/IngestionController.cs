using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IngestionController : ControllerBase
    {
        private readonly AppDb _db;
        private readonly OpenAIClient _openAI;

        public IngestionController(AppDb db, OpenAIClient openAI)
        {
            _db = db;
            _openAI = openAI;
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
                var chatClient = _openAI.GetChatClient("gpt-4.1-mini");

                var response = await chatClient.CompleteChatAsync(new[]
                {
                    ChatMessage.CreateUserMessage(new ChatMessageContentPart[]
                    {
                        ChatMessageContentPart.CreateTextPart(
                            "Extract the recipe name, servings, and ingredients as structured JSON. " +
                            "Use this schema: { title: string, servings: number, ingredients: [ { name: string, quantity: string } ] }"
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

                var recipe = new Recipe
                {
                    Id = Guid.NewGuid(),
                    Title = extracted.Title,
                    Servings = extracted.Servings,
                    RecipeIngredients = new List<RecipeIngredient>()
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
                    return BadRequest("No files uploaded.");     var chatClient = _openAI.GetChatClient("gpt-4.1-mini");
            
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

                    var response = await chatClient.CompleteChatAsync(new[]
                    {
                        ChatMessage.CreateUserMessage(new ChatMessageContentPart[]
                        {
                            ChatMessageContentPart.CreateTextPart(
                                "Extract the recipe name, servings, and ingredients as structured JSON. " +
                                "Use this schema: { title: string, servings: number, ingredients: [ { name: string, quantity: string } ] }"
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
                        RecipeIngredients = new List<RecipeIngredient>()
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
    }
}
