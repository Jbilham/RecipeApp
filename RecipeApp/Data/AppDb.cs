using Microsoft.EntityFrameworkCore;
using RecipeApp.Models;

namespace RecipeApp.Data
{
    public class AppDb : DbContext
    {
        public AppDb(DbContextOptions<AppDb> options) : base(options) {}

        public DbSet<Recipe> Recipes => Set<Recipe>();
        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<Unit> Units => Set<Unit>();
        public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
        public DbSet<Tag> Tags => Set<Tag>();
        public DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();

        public DbSet<MealPlan> MealPlans => Set<MealPlan>();
        public DbSet<Meal> Meals => Set<Meal>();
        public DbSet<ShoppingListSnapshot> ShoppingListSnapshots { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 🔗 MealPlan ↔ Meal (1-to-many)
            modelBuilder.Entity<Meal>()
                .HasOne(m => m.MealPlan)
                .WithMany(mp => mp.Meals)
                .HasForeignKey(m => m.MealPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🔗 Meal ↔ Recipe (many-to-one)
            modelBuilder.Entity<Meal>()
                .HasOne(m => m.Recipe)
                .WithMany()
                .HasForeignKey(m => m.RecipeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ Composite key for RecipeTag
            modelBuilder.Entity<RecipeTag>()
                .HasKey(rt => new { rt.RecipeId, rt.TagId });

            // ✅ Seed Units
            modelBuilder.Entity<Unit>().HasData(
                new Unit { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Code = "g", ToGramsFactor = 1, IsMass = true },
                new Unit { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Code = "kg", ToGramsFactor = 1000, IsMass = true },
                new Unit { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Code = "ml", ToMillilitersFactor = 1, IsMass = false },
                new Unit { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Code = "l", ToMillilitersFactor = 1000, IsMass = false },
                new Unit { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Code = "tsp", ToMillilitersFactor = 5, IsMass = false },
                new Unit { Id = Guid.Parse("66666666-6666-6666-6666-666666666666"), Code = "tbsp", ToMillilitersFactor = 15, IsMass = false },
                new Unit { Id = Guid.Parse("77777777-7777-7777-7777-777777777777"), Code = "cup", ToMillilitersFactor = 240, IsMass = false },
                new Unit { Id = Guid.Parse("88888888-8888-8888-8888-888888888888"), Code = "item", IsMass = false }
            );
        }
    }
}
