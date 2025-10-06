using System;

namespace RecipeApp.Models
{
    public class Unit
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";   // "g","ml","tsp"
        public decimal? ToGramsFactor { get; set; }
        public decimal? ToMillilitersFactor { get; set; }
        public bool IsMass { get; set; }
    }
}
