using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMealAndRecipeNutrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Calories",
                table: "Recipes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Carbs",
                table: "Recipes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fat",
                table: "Recipes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MacrosEstimated",
                table: "Recipes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Protein",
                table: "Recipes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Calories",
                table: "Meals",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Carbs",
                table: "Meals",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fat",
                table: "Meals",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NutritionEstimated",
                table: "Meals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NutritionSource",
                table: "Meals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Protein",
                table: "Meals",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Calories",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Carbs",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Fat",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "MacrosEstimated",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Protein",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Calories",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "Carbs",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "Fat",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "NutritionEstimated",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "NutritionSource",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "Protein",
                table: "Meals");
        }
    }
}
