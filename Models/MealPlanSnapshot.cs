using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RecipeApp.Models
{
    public class MealPlanSnapshot
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? ShoppingListSnapshotId { get; set; }

        [ForeignKey(nameof(ShoppingListSnapshotId))]
        public ShoppingListSnapshot? ShoppingListSnapshot { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid? CreatedById { get; set; }

        [ForeignKey(nameof(CreatedById))]
        public AppUser? CreatedBy { get; set; }

        public DateTime? WeekStart { get; set; }
        public DateTime? WeekEnd { get; set; }
        public string Range { get; set; } = string.Empty;

        public string JsonData { get; set; } = string.Empty;
        public string SourceType { get; set; } = "unknown";
    }
}
