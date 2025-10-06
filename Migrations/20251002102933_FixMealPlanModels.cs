using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeApp.Migrations
{
    /// <inheritdoc />
    public partial class FixMealPlanModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Meals_MealPlans_MealPlanId",
                table: "Meals");

            migrationBuilder.DropTable(
                name: "RecipeMeal");

            migrationBuilder.AlterColumn<string>(
                name: "MealType",
                table: "Meals",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MealPlanId",
                table: "Meals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipeId",
                table: "Meals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meals_RecipeId",
                table: "Meals",
                column: "RecipeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Meals_MealPlans_MealPlanId",
                table: "Meals",
                column: "MealPlanId",
                principalTable: "MealPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Meals_Recipes_RecipeId",
                table: "Meals",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Meals_MealPlans_MealPlanId",
                table: "Meals");

            migrationBuilder.DropForeignKey(
                name: "FK_Meals_Recipes_RecipeId",
                table: "Meals");

            migrationBuilder.DropIndex(
                name: "IX_Meals_RecipeId",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "RecipeId",
                table: "Meals");

            migrationBuilder.AlterColumn<string>(
                name: "MealType",
                table: "Meals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "MealPlanId",
                table: "Meals",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "RecipeMeal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeMeal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeMeal_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeMeal_MealId",
                table: "RecipeMeal",
                column: "MealId");

            migrationBuilder.AddForeignKey(
                name: "FK_Meals_MealPlans_MealPlanId",
                table: "Meals",
                column: "MealPlanId",
                principalTable: "MealPlans",
                principalColumn: "Id");
        }
    }
}
