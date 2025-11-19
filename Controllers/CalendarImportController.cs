using Microsoft.AspNetCore.Mvc;
using RecipeApp.Services;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CalendarImportController : ControllerBase
    {
        private readonly IUserContext _userContext;
        private readonly ICalendarImportService _calendarImportService;

        public CalendarImportController(IUserContext userContext, ICalendarImportService calendarImportService)
        {
            _userContext = userContext;
            _calendarImportService = calendarImportService;
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportCalendar(
            [FromQuery] string url,
            [FromQuery] string range = "this")
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest("No calendar URL provided.");

            try
            {
                var currentUser = await _userContext.GetCurrentUserAsync();
                var result = await _calendarImportService.ImportAsync(currentUser, url, range);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
