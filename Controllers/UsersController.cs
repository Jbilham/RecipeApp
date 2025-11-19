using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Dtos;
using RecipeApp.Models;
using RecipeApp.Services;
using System.Linq.Expressions;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserContext _userContext;
        private readonly UserManager<AppUser> _userManager;

        public UsersController(IUserContext userContext, UserManager<AppUser> userManager)
        {
            _userContext = userContext;
            _userManager = userManager;
        }

        [HttpGet("me")]
        public async Task<ActionResult<object>> GetCurrentUserAsync()
        {
            var currentUser = await _userContext.GetCurrentUserAsync();

            var children = await _userManager.Users
                .Where(u => u.ParentUserId == currentUser.Id)
                .Select(ToSummary())
                .ToListAsync();

            return Ok(new
            {
                user = ToSummary().Compile()(currentUser),
                children
            });
        }

        [HttpPost("nutritionists")]
        public async Task<IActionResult> CreateNutritionist([FromBody] CreateNutritionistDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest("Email is required.");

            var currentUser = await _userContext.GetCurrentUserAsync();
            if (!string.Equals(currentUser.Role, "Master", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
                return Conflict("A user with that email already exists.");

            var nutritionist = new AppUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                EmailConfirmed = true,
                PhoneNumber = dto.PhoneNumber,
                Role = "Nutritionist",
                ParentUserId = currentUser.Id
            };

            var createResult = await _userManager.CreateAsync(nutritionist, dto.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return BadRequest(errors);
            }

            await _userManager.AddToRoleAsync(nutritionist, "Nutritionist");

            return CreatedAtAction(nameof(GetCurrentUserAsync), new { id = nutritionist.Id }, ToSummary().Compile()(nutritionist));
        }

        [HttpPost("clients")]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest("Email is required.");

            var currentUser = await _userContext.GetCurrentUserAsync();

            Guid? nutritionistId = dto.NutritionistId;

            if (string.Equals(currentUser.Role, "Master", StringComparison.OrdinalIgnoreCase))
            {
                if (nutritionistId == null)
                    return BadRequest("NutritionistId is required when creating clients as Master.");
            }
            else if (string.Equals(currentUser.Role, "Nutritionist", StringComparison.OrdinalIgnoreCase))
            {
                nutritionistId ??= currentUser.Id;
                if (nutritionistId != currentUser.Id)
                    return Forbid();
            }
            else
            {
                return Forbid();
            }

            var nutritionist = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == nutritionistId);
            if (nutritionist == null || !string.Equals(nutritionist.Role, "Nutritionist", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Nutritionist not found.");

            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
                return Conflict("A user with that email already exists.");

            var client = new AppUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                EmailConfirmed = true,
                PhoneNumber = dto.PhoneNumber,
                Role = "Client",
                ParentUserId = nutritionist.Id
            };

            var createResult = await _userManager.CreateAsync(client, dto.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return BadRequest(errors);
            }

            await _userManager.AddToRoleAsync(client, "Client");

            return CreatedAtAction(nameof(GetCurrentUserAsync), new { id = client.Id }, ToSummary().Compile()(client));
        }

        [HttpGet("clients")]
        public async Task<ActionResult<IEnumerable<UserSummaryDto>>> GetClientsAsync()
        {
            var currentUser = await _userContext.GetCurrentUserAsync();

            if (string.Equals(currentUser.Role, "Master", StringComparison.OrdinalIgnoreCase))
            {
                var clients = await _userManager.Users
                    .Where(u => u.Role == "Client")
                    .Select(ToSummary())
                    .ToListAsync();
                return Ok(clients);
            }

            if (string.Equals(currentUser.Role, "Nutritionist", StringComparison.OrdinalIgnoreCase))
            {
                var clients = await _userManager.Users
                    .Where(u => u.Role == "Client" && u.ParentUserId == currentUser.Id)
                    .Select(ToSummary())
                    .ToListAsync();
                return Ok(clients);
            }

            return Forbid();
        }

        private static Expression<Func<AppUser, UserSummaryDto>> ToSummary() => user => new UserSummaryDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Role = user.Role,
            ParentUserId = user.ParentUserId,
            TrainingPeaksIcsUrl = user.TrainingPeaksIcsUrl
        };
    }
}
