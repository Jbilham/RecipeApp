using Microsoft.AspNetCore.Identity;

namespace RecipeApp.Models
{
    public class AppUser : IdentityUser<Guid>
    {
        public string Role { get; set; } = "Client";

        public Guid? ParentUserId { get; set; }
        public AppUser? ParentUser { get; set; }

        public ICollection<AppUser> Children { get; set; } = new List<AppUser>();

        public string? TrainingPeaksIcsUrl { get; set; }
    }
}
