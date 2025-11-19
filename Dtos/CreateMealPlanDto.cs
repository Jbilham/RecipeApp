

namespace RecipeApp.Dtos
{
    public class CreateMealPlanDto
    {
        public string Name { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public Guid? AssignedUserId { get; set; }

        // ✅ allows full text plan paste (used by parser)
        public string? FreeText { get; set; }

        public List<CreateMealDto> Meals { get; set; } = new();
    }

    public class CreateMealDto
    {
        public string MealType { get; set; } = string.Empty;
        public Guid? RecipeId { get; set; }
        public string? FreeText { get; set; }
    }
}
