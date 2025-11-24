using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace RecipeApp.Models
{
    public class MealPlan
    {
        [Key]
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;  // e.g. "Week 1 Day 1"

        public Guid? CreatedById { get; set; }
        public AppUser? CreatedBy { get; set; }

        public Guid? AssignedToId { get; set; }
        public AppUser? AssignedTo { get; set; }

        [Column(TypeName = "timestamp without time zone")]
        public DateTime? Date { get; set; }

        public ICollection<Meal> Meals { get; set; } = new List<Meal>();

        // 🧩 Changed: store FreeItems as serialized JSON text, not List<string>
        [Column(TypeName = "text")]
        public string? FreeItemsJson { get; set; }

        // Helper property (not mapped) for code convenience
        [NotMapped]
        public List<string> FreeItems
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FreeItemsJson))
                    return new List<string>();
                try
                {
                    return JsonSerializer.Deserialize<List<string>>(FreeItemsJson) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                FreeItemsJson = JsonSerializer.Serialize(value ?? new List<string>());
            }
        }
    }

    public class Meal
    {
        public Guid Id { get; set; }
        public Guid MealPlanId { get; set; }
        public MealPlan MealPlan { get; set; } = null!;

        public string MealType { get; set; } = "";  // e.g. Breakfast, Lunch
        public Guid? RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        public string? FreeText { get; set; } // "Protein shake + banana"

        [Column(TypeName = "text")]
        public string? ExtraItemsJson { get; set; }

        public decimal? Calories { get; set; }
        public decimal? Protein { get; set; }
        public decimal? Carbs { get; set; }
        public decimal? Fat { get; set; }
        public string? NutritionSource { get; set; }
        public bool NutritionEstimated { get; set; }

        [NotMapped]
        public List<string> ExtraItems
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ExtraItemsJson))
                    return new List<string>();
                try
                {
                    return JsonSerializer.Deserialize<List<string>>(ExtraItemsJson) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                ExtraItemsJson = JsonSerializer.Serialize(value ?? new List<string>());
            }
        }

        public bool IsSelected { get; set; } = true;
    }
}
