namespace RecipeApp.Dtos
{
    public class CreateNutritionistDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = "Nutritionist!123";
        public string? PhoneNumber { get; set; }
    }

    public class CreateClientDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = "Client!123";
        public Guid? NutritionistId { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class UserSummaryDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid? ParentUserId { get; set; }
        public string? TrainingPeaksIcsUrl { get; set; }
    }
}
