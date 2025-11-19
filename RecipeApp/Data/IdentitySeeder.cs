using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Models;

namespace RecipeApp.Data
{
    public static class IdentitySeeder
    {
        private static readonly string[] Roles = new[] { "Master", "Nutritionist", "Client" };

        public static async Task SeedIdentityAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            foreach (var role in Roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                }
            }

            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var configuration = services.GetRequiredService<IConfiguration>();

            var masterEmail = configuration["Seed:MasterEmail"] ?? "master@recipeapp.local";
            var masterPassword = configuration["Seed:MasterPassword"] ?? "Master!123";
            var masterUser = await EnsureUserAsync(userManager, masterEmail, masterPassword, "Master");

            var nutritionistEmail = configuration["Seed:NutritionistEmail"] ?? "nutritionist@recipeapp.local";
            var nutritionistPassword = configuration["Seed:NutritionistPassword"] ?? "Nutritionist!123";
            var nutritionistUser = await EnsureUserAsync(
                userManager,
                nutritionistEmail,
                nutritionistPassword,
                "Nutritionist",
                parentUserId: masterUser.Id);

            var clientEmail = configuration["Seed:ClientEmail"] ?? "client@recipeapp.local";
            var clientPassword = configuration["Seed:ClientPassword"] ?? "Client!123";
            await EnsureUserAsync(
                userManager,
                clientEmail,
                clientPassword,
                "Client",
                parentUserId: nutritionistUser.Id);
        }

        private static async Task<AppUser> EnsureUserAsync(
            UserManager<AppUser> userManager,
            string email,
            string password,
            string role,
            Guid? parentUserId = null)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new AppUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    Role = role,
                    ParentUserId = parentUserId
                };

                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to create {role} user '{email}': {errors}");
                }
            }

            var requiresUpdate = false;

            if (!string.Equals(user.Role, role, StringComparison.OrdinalIgnoreCase))
            {
                user.Role = role;
                requiresUpdate = true;
            }

            if (user.ParentUserId != parentUserId)
            {
                user.ParentUserId = parentUserId;
                requiresUpdate = true;
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                requiresUpdate = true;
            }

            if (requiresUpdate)
            {
                await userManager.UpdateAsync(user);
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }

            return user;
        }
    }
}
