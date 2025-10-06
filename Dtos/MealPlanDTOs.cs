namespace RecipeApp.Dtos
{
    public class MealPlanDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public List<MealDto> Meals { get; set; } = new();

        // ✅ Extra free-text items extracted during parsing
        public List<string> FreeItems { get; set; } = new();
    }

    public class MealDto
  
    {
        public Guid Id { get; set; }

        public string MealType { get; set; } = string.Empty;

        public Guid? RecipeId { get; set; }

        public string? RecipeName { get; set; }

        public string? FreeText { get; set; }
    }
}
