namespace RecipeApp.Dtos
{
    public class LoginRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public UserSummaryDto User { get; set; } = new UserSummaryDto();
    }
}
