using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFreeItemsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeItems",
                table: "MealPlans");

            migrationBuilder.AddColumn<string>(
                name: "FreeItemsJson",
                table: "MealPlans",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeItemsJson",
                table: "MealPlans");

            migrationBuilder.AddColumn<List<string>>(
                name: "FreeItems",
                table: "MealPlans",
                type: "jsonb",
                nullable: true);
        }
    }
}
