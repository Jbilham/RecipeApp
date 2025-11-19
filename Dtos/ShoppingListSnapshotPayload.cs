using System;
using System.Collections.Generic;

namespace RecipeApp.Dtos
{
    public class ShoppingListSnapshotPayload
    {
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public string Range { get; set; } = string.Empty;
        public List<Guid> MealPlanIds { get; set; } = new();
        public List<ShoppingListPlanSummary> Plans { get; set; } = new();
        public ShoppingListResponse ShoppingList { get; set; } = new();
        public Guid? MealPlanSnapshotId { get; set; }
    }

    public class ShoppingListPlanSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
    }
}
