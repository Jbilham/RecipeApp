using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RecipeApp.Migrations
{
    /// <inheritdoc />
    public partial class SeedUnitsFixed2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("1271ff5e-c9b8-4567-83bf-d7d86bddbfec"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("3885fcf3-1eab-4bb3-b18d-ae0ca18c8e89"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("39e1264b-cad0-4253-9905-fd525a62fe0a"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("997350f6-687f-4360-8336-d954c55c28df"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("a322b769-51c7-4673-aaf3-8ea89a5755a1"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("d0763c7a-e61f-4348-bbc5-ceadf364bb07"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("eeedea49-7980-4a45-b18f-43e77a045ed1"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("f224d612-13ca-4c82-9e82-76636df36f93"));

            migrationBuilder.InsertData(
                table: "Units",
                columns: new[] { "Id", "Code", "IsMass", "ToGramsFactor", "ToMillilitersFactor" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "g", true, 1m, null },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "kg", true, 1000m, null },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "ml", false, null, 1m },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "l", false, null, 1000m },
                    { new Guid("55555555-5555-5555-5555-555555555555"), "tsp", false, null, 5m },
                    { new Guid("66666666-6666-6666-6666-666666666666"), "tbsp", false, null, 15m },
                    { new Guid("77777777-7777-7777-7777-777777777777"), "cup", false, null, 240m },
                    { new Guid("88888888-8888-8888-8888-888888888888"), "item", false, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"));

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"));

            migrationBuilder.InsertData(
                table: "Units",
                columns: new[] { "Id", "Code", "IsMass", "ToGramsFactor", "ToMillilitersFactor" },
                values: new object[,]
                {
                    { new Guid("1271ff5e-c9b8-4567-83bf-d7d86bddbfec"), "cup", false, null, 240m },
                    { new Guid("3885fcf3-1eab-4bb3-b18d-ae0ca18c8e89"), "item", false, null, null },
                    { new Guid("39e1264b-cad0-4253-9905-fd525a62fe0a"), "tsp", false, null, 5m },
                    { new Guid("997350f6-687f-4360-8336-d954c55c28df"), "ml", false, null, 1m },
                    { new Guid("a322b769-51c7-4673-aaf3-8ea89a5755a1"), "tbsp", false, null, 15m },
                    { new Guid("d0763c7a-e61f-4348-bbc5-ceadf364bb07"), "l", false, null, 1000m },
                    { new Guid("eeedea49-7980-4a45-b18f-43e77a045ed1"), "g", true, 1m, null },
                    { new Guid("f224d612-13ca-4c82-9e82-76636df36f93"), "kg", true, 1000m, null }
                });
        }
    }
}
