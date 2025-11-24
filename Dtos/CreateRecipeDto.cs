namespace RecipeApp.Dtos
{
    public class CreateRecipeDto
    {
        public string Title { get; set; } = string.Empty;
        public int Servings { get; set; }
        public bool IsGlobal { get; set; }
        public Guid? AssignedUserId { get; set; }

        public decimal? Calories { get; set; }
        public decimal? Protein { get; set; }
        public decimal? Carbs { get; set; }
        public decimal? Fat { get; set; }

        public List<CreateRecipeIngredientDto> Ingredients { get; set; } = new();
    }
}
