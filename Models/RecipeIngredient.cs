namespace RecipeApp.Models
{
    public class RecipeIngredient
    {
        public Guid Id { get; set; }
        public Guid RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public Guid IngredientId { get; set; }
        public Ingredient Ingredient { get; set; } = null!;

        public decimal? Amount { get; set; }

        // link to Units table
        public Guid? UnitId { get; set; }
        public Unit? Unit { get; set; }

        // optional free-text notes
        public string? Notes { get; set; }
    }
}
