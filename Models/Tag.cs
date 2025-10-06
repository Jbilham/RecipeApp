using System;
using System.Collections.Generic;

namespace RecipeApp.Models
{
    public class Tag
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public List<RecipeTag> RecipeTags { get; set; } = new();
    }

    public class RecipeTag
    {
        public Guid RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;
        public Guid TagId { get; set; }
        public Tag Tag { get; set; } = null!;
    }
}
