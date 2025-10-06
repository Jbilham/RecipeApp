namespace RecipeApp.Dtos
{
    public class CreateWeeklyMealPlanDto
    {
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        // Each day's free text input
        public string? Monday { get; set; }
        public string? Tuesday { get; set; }
        public string? Wednesday { get; set; }
        public string? Thursday { get; set; }
        public string? Friday { get; set; }
        public string? Saturday { get; set; }
        public string? Sunday { get; set; }
    }
}
