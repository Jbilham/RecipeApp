using System;
using System.Collections.Generic;

namespace RecipeApp.Models
{
    public class Recipe
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public int Servings { get; set; }

        public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
        public ICollection<RecipeTag> RecipeTags { get; set; } = new List<RecipeTag>();
    }
}

