using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Models;

namespace RecipeApp.Services
{
    public interface IUserContext
    {
        Task<AppUser> GetCurrentUserAsync();
        Task<Guid> GetCurrentUserIdAsync();
        Task<HashSet<Guid>> GetVisibleUserIdsAsync();
    }

    public class UserContext : IUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<AppUser> _userManager;

        public UserContext(IHttpContextAccessor httpContextAccessor, UserManager<AppUser> userManager)
        {
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
        }

        public async Task<AppUser> GetCurrentUserAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext != null)
            {
                var principal = httpContext.User;
                if (principal?.Identity?.IsAuthenticated == true)
                {
                    var user = await _userManager.GetUserAsync(principal);
                    if (user != null)
                    {
                        return user;
                    }
                }

                if (httpContext.Request.Headers.TryGetValue("X-User-Id", out var headerValues))
                {
                    var headerUserId = headerValues.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(headerUserId) && Guid.TryParse(headerUserId, out var headerGuid))
                    {
                        var headerUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == headerGuid);
                        if (headerUser != null)
                        {
                            return headerUser;
                        }
                    }
                }
            }

            var master = await _userManager.Users.FirstOrDefaultAsync(u => u.Role == "Master");
            if (master == null)
            {
                throw new InvalidOperationException("Master user has not been seeded in the system.");
            }

            return master;
        }

        public async Task<Guid> GetCurrentUserIdAsync()
        {
            var user = await GetCurrentUserAsync();
            return user.Id;
        }

        public async Task<HashSet<Guid>> GetVisibleUserIdsAsync()
        {
            var currentUser = await GetCurrentUserAsync();

            if (string.Equals(currentUser.Role, "Master", StringComparison.OrdinalIgnoreCase))
            {
                return await _userManager.Users.Select(u => u.Id).ToHashSetAsync();
            }

            var ids = new HashSet<Guid> { currentUser.Id };

            if (string.Equals(currentUser.Role, "Nutritionist", StringComparison.OrdinalIgnoreCase))
            {
                var childIds = await _userManager.Users
                    .Where(u => u.ParentUserId == currentUser.Id)
                    .Select(u => u.Id)
                    .ToListAsync();

                foreach (var id in childIds)
                {
                    ids.Add(id);
                }
            }

            return ids;
        }
    }
}
