namespace RecipeApp.Dtos
{
 public class ExtractRecipeDto
    {
        public string Title { get; set; } = string.Empty;
        public int Servings { get; set; }
        public decimal? Calories { get; set; }
        public decimal? Protein { get; set; }
        public decimal? Carbs { get; set; }
        public decimal? Fat { get; set; }
        public List<ExtractRecipeIngredientDto> Ingredients { get; set; } = new();
    }

       public class ExtractRecipeIngredientDto
    {
        public string Name { get; set; } = string.Empty;   // ✅ stays Name (matches AI JSON)
        public string? Quantity { get; set; }
        public decimal? Amount { get; set; }
        public string? Unit { get; set; }
        public string? Notes { get; set; }
    }
}
