using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RecipeApp.Models
{
    public class ShoppingListSnapshot
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? MealPlanId { get; set; }

        [ForeignKey(nameof(MealPlanId))]
        public MealPlan? MealPlan { get; set; }

        public Guid? MealPlanSnapshotId { get; set; }

        [ForeignKey(nameof(MealPlanSnapshotId))]
        public MealPlanSnapshot? MealPlanSnapshot { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid? CreatedById { get; set; }

        [ForeignKey(nameof(CreatedById))]
        public AppUser? CreatedBy { get; set; }

        // Store as serialized JSON text
        public string JsonData { get; set; } = string.Empty;

        // Optional: identify source (single, week, manual)
        public string SourceType { get; set; } = "unknown";
    }
}
