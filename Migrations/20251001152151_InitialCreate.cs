using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Recipes");

            migrationBuilder.AlterColumn<int>(
                name: "Servings",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Ingredients",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Ingredients");

            migrationBuilder.AlterColumn<int>(
                name: "Servings",
                table: "Recipes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Recipes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Recipes",
                type: "text",
                nullable: true);
        }
    }
}
