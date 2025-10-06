using System;
using System.Collections.Generic;

namespace RecipeApp.Dtos
{
  
    public class ShoppingListRequest
    {
        public List<Guid> RecipeIds { get; set; } = new();

        // ✅ new property for "extra" free-text items like fruit, chocolate, etc.
        public List<string> FreeItems { get; set; } = new();
    }

    public class ShoppingListItemDto
    {
        public string Ingredient { get; set; } = "";
        public decimal? Amount { get; set; }
        public string? Unit { get; set; }
        public List<Guid> SourceRecipes { get; set; } = new();
    }

    public class ShoppingListResponse
    {
        public List<ShoppingListItemDto> Items { get; set; } = new();
    }
}
