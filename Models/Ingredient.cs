using System;
using System.Collections.Generic;

namespace RecipeApp.Models
{
    public class Ingredient
    {
        public Guid Id { get; set; }

        // Keep old property for backwards compatibility
        public string Name { get; set; } = string.Empty;

        // New fields for canonicalized + user-friendly naming
        public string NameCanonical { get; set; } = string.Empty;  // e.g. "apple"
        public string NameDisplay { get; set; } = string.Empty;    // e.g. "Granny Smith Apple"

        public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
    }
}
