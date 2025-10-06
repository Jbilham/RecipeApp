namespace RecipeApp.Dtos
{
    public class RecipeIngredientDto
    {
        public string Ingredient { get; set; } = string.Empty;  // ✅ controller expects this
        public string Name { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string? Unit { get; set; }
        public string? Notes { get; set; }
    }
}