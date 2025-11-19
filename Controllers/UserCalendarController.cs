using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Dtos;
using RecipeApp.Models;
using RecipeApp.Services;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/usercalendar")]
    public class UserCalendarController : ControllerBase
    {
        private readonly IUserContext _userContext;
        private readonly UserManager<AppUser> _userManager;
        private readonly ICalendarImportService _calendarImportService;

        public UserCalendarController(
            IUserContext userContext,
            UserManager<AppUser> userManager,
            ICalendarImportService calendarImportService)
        {
            _userContext = userContext;
            _userManager = userManager;
            _calendarImportService = calendarImportService;
        }

        [HttpGet("get-url")]
        public async Task<IActionResult> GetUrl([FromQuery] Guid? userId = null)
        {
            var currentUser = await _userContext.GetCurrentUserAsync();
            var targetUser = await ResolveTargetUserAsync(currentUser, userId);
            if (targetUser == null)
            {
                return Forbid();
            }

            return Ok(new { url = targetUser.TrainingPeaksIcsUrl });
        }

        [HttpPost("set-url")]
        public async Task<IActionResult> SetUrl([FromBody] SetTrainingPeaksUrlDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Url))
                return BadRequest("A valid TrainingPeaks ICS URL is required.");

            var currentUser = await _userContext.GetCurrentUserAsync();
            var targetUser = await ResolveTargetUserAsync(currentUser, dto.UserId);
            if (targetUser == null)
            {
                return Forbid();
            }

            targetUser.TrainingPeaksIcsUrl = dto.Url.Trim();
            await _userManager.UpdateAsync(targetUser);

            return NoContent();
        }

        [HttpPost("import")]
        public async Task<IActionResult> Import([FromQuery] string range = "this", [FromBody] ImportTrainingPeaksDto? dto = null)
        {
            var currentUser = await _userContext.GetCurrentUserAsync();
            var targetUser = await ResolveTargetUserAsync(currentUser, dto?.UserId);
            if (targetUser == null)
            {
                return Forbid();
            }

            var url = dto?.UrlOverride ?? targetUser.TrainingPeaksIcsUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                return BadRequest("No TrainingPeaks ICS URL is stored for the selected user.");
            }

            try
            {
                var result = await _calendarImportService.ImportAsync(targetUser, url, range);
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

        private async Task<AppUser?> ResolveTargetUserAsync(AppUser currentUser, Guid? targetUserId)
        {
            if (targetUserId == null || targetUserId == currentUser.Id)
            {
                return currentUser;
            }

            if (string.Equals(currentUser.Role, "Master", StringComparison.OrdinalIgnoreCase))
            {
                return await _userManager.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
            }

            if (string.Equals(currentUser.Role, "Nutritionist", StringComparison.OrdinalIgnoreCase))
            {
                var child = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
                if (child != null && (child.ParentUserId == currentUser.Id || child.Id == currentUser.Id))
                {
                    return child;
                }
            }

            return null;
        }
    }
}
