using Microsoft.AspNetCore.Mvc;
using OpenAI;
using OpenAI.Chat;
using RecipeApp.Dtos;
using RecipeApp.Data;
using RecipeApp.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IngestionController : ControllerBase
    {
        private readonly OpenAIClient _client;
        private readonly AppDb _db;

        public IngestionController(IConfiguration config, AppDb db)
        {
            var apiKey = config["OpenAI:ApiKey"];
            _client = new OpenAIClient(apiKey);
            _db = db;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] UploadRecipeImageDto request)
        {
            var file = request.File;
            if (file is null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var tempPath = Path.GetTempFileName();
            await using (var stream = System.IO.File.Create(tempPath))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                // Read file bytes directly
                var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);

                var chatClient = _client.GetChatClient("gpt-4.1-mini");

                var response = await chatClient.CompleteChatAsync(new[]
                {
                    ChatMessage.CreateUserMessage(new ChatMessageContentPart[]
                    {
                        ChatMessageContentPart.CreateTextPart(
                            "Extract the recipe name, servings, and ingredients as structured JSON. " +
                            "Use this schema: { title: string, servings: number, ingredients: [ { name: string, quantity: string } ] }"
                        ),
                        ChatMessageContentPart.CreateImagePart(new BinaryData(bytes), "image/png")
                    })
                });

                var content = response.Value.Content[0].Text;

                // Clean JSON response
                var json = content.Replace("```json", "").Replace("```", "");

                var extracted = JsonSerializer.Deserialize<ExtractRecipeDto>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (extracted == null)
                    return BadRequest("Could not parse recipe from AI output.");

                // Map DTO -> Entities
                var recipe = new Recipe
                {
                    Id = Guid.NewGuid(),
                    Title = extracted.Title,
                    Servings = extracted.Servings,
                    RecipeIngredients = new List<RecipeIngredient>()
                };

                foreach (var i in extracted.Ingredients)
                {
                    // Parse quantity string into amount + unit
                    decimal? amount = null;
                    string? unitStr = null;

                    if (!string.IsNullOrWhiteSpace(i.Quantity))
                    {
                        (amount, unitStr) = ParseQuantity(i.Quantity);
                    }

                    Unit? unitEntity = null;
                    if (!string.IsNullOrWhiteSpace(unitStr))
                        unitEntity = _db.Units.FirstOrDefault(u => u.Code == unitStr);

                    var ingredient = new Ingredient
                    {
                        Id = Guid.NewGuid(),
                        Name = i.Name
                    };

                    var recipeIngredient = new RecipeIngredient
                    {
                        Id = Guid.NewGuid(),
                        RecipeId = recipe.Id,
                        Ingredient = ingredient,
                        Amount = amount,
                        Unit = unitEntity,
                        Notes = i.Notes ?? i.Quantity
                    };

                    recipe.RecipeIngredients.Add(recipeIngredient);
                }

                _db.Recipes.Add(recipe);
                    await _db.SaveChangesAsync();

                    // Project to DTO to avoid EF Core cycles
                    var recipeDto = new RecipeResponseDto
                    {
                        RecipeId = recipe.Id,
                        Title = recipe.Title,
                        Servings = recipe.Servings,
                        Ingredients = recipe.RecipeIngredients.Select(ri => new RecipeIngredientResponseDto
                        {
                            Ingredient = ri.Ingredient.Name,
                            Amount = ri.Amount,
                            Unit = ri.Unit?.Code,
                            Notes = ri.Notes
                        }).ToList()
                    };

                    return Ok(new { recipe = recipeDto, aiRaw = content });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "OCR failed", details = ex.ToString() });
            }
            finally
            {
                try { System.IO.File.Delete(tempPath); } catch { }
            }
        }

        /// <summary>
        /// Very simple parser: splits a string like "120ml" or "1/2 tsp"
        /// into numeric amount and unit string.
        /// </summary>
        private (decimal?, string?) ParseQuantity(string quantity)
        {
            if (string.IsNullOrWhiteSpace(quantity))
                return (null, null);

            // Match "number+unit" e.g. "120ml", "1/2 tsp"
            var match = Regex.Match(quantity.Trim(), @"^([\d\/\.]+)\s*([a-zA-Z]+)?");

            if (!match.Success)
                return (null, quantity); // fallback to notes

            decimal? amount = null;
            var numPart = match.Groups[1].Value;

            // Handle fractions like "1/2"
            if (numPart.Contains('/'))
            {
                var parts = numPart.Split('/');
                if (parts.Length == 2 &&
                    decimal.TryParse(parts[0], out var numerator) &&
                    decimal.TryParse(parts[1], out var denominator) &&
                    denominator != 0)
                {
                    amount = numerator / denominator;
                }
            }
            else if (decimal.TryParse(numPart, out var parsed))
            {
                amount = parsed;
            }

            var unit = match.Groups[2].Success ? match.Groups[2].Value : null;

            return (amount, unit);
        }
    }
}
