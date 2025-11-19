using System;
using System.Collections.Generic;

namespace RecipeApp.Models
{
    public class Recipe
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public int Servings { get; set; }

        public Guid? OwnerId { get; set; }
        public AppUser? Owner { get; set; }

        public bool IsGlobal { get; set; }

        public Guid? AssignedToId { get; set; }
        public AppUser? AssignedTo { get; set; }

        public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
        public ICollection<RecipeTag> RecipeTags { get; set; } = new List<RecipeTag>();
    }
}
