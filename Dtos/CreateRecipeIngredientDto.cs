namespace RecipeApp.Dtos
{
    public class CreateRecipeIngredientDto
    {
        public string Ingredient { get; set; } = string.Empty;  // ✅ Add this
        public decimal? Amount { get; set; }
        public string? Unit { get; set; }
        public string? Notes { get; set; }
    }
}
