namespace RecipeApp.Dtos
{
    public class RecipeResponseDto
    {
        public Guid RecipeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Servings { get; set; }
        public decimal? Calories { get; set; }
        public decimal? Protein { get; set; }
        public decimal? Carbs { get; set; }
        public decimal? Fat { get; set; }
        public List<RecipeIngredientResponseDto> Ingredients { get; set; } = new();
    }

    public class RecipeIngredientResponseDto
    {
        public string Ingredient { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string? Unit { get; set; }
        public string? Notes { get; set; }
    }
}
