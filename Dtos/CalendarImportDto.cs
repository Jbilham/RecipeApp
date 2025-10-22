using Microsoft.AspNetCore.Http;

namespace RecipeApp.Dtos
{
    public class CalendarImportDto
    {
        public IFormFile File { get; set; } = default!;
    }
}
