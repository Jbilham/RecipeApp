namespace RecipeApp.Dtos
{
    public class RecipeDto
    {
        public Guid RecipeId { get; set; }      // ✅ Add this
        public string Title { get; set; } = string.Empty;
        public int Servings { get; set; }
        public decimal? Calories { get; set; }
        public decimal? Protein { get; set; }
        public decimal? Carbs { get; set; }
        public decimal? Fat { get; set; }
        public List<RecipeIngredientDto> Ingredients { get; set; } = new();
    }
}
