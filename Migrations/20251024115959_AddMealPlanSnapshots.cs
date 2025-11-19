using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"ShoppingListSnapshots\" ADD COLUMN IF NOT EXISTS \"MealPlanSnapshotId\" uuid NULL;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""MealPlanSnapshots"" (
    ""Id"" uuid PRIMARY KEY,
    ""ShoppingListSnapshotId"" uuid NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""WeekStart"" timestamp with time zone NULL,
    ""WeekEnd"" timestamp with time zone NULL,
    ""Range"" text NOT NULL,
    ""JsonData"" text NOT NULL,
    ""SourceType"" text NOT NULL
);");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ShoppingListSnapshots_MealPlanSnapshotId\" ON \"ShoppingListSnapshots\" (\"MealPlanSnapshotId\");");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_MealPlanSnapshots_ShoppingListSnapshotId\" ON \"MealPlanSnapshots\" (\"ShoppingListSnapshotId\");");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_ShoppingListSnapshots_MealPlanSnapshots_MealPlanSnapshotId'
    ) THEN
        ALTER TABLE ""ShoppingListSnapshots""
        ADD CONSTRAINT ""FK_ShoppingListSnapshots_MealPlanSnapshots_MealPlanSnapshotId""
        FOREIGN KEY (""MealPlanSnapshotId"") REFERENCES ""MealPlanSnapshots""(""Id"") ON DELETE SET NULL;
    END IF;
END$$;");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_MealPlanSnapshots_ShoppingListSnapshots_ShoppingListSnapshotId'
    ) THEN
        ALTER TABLE ""MealPlanSnapshots""
        ADD CONSTRAINT ""FK_MealPlanSnapshots_ShoppingListSnapshots_ShoppingListSnapshotId""
        FOREIGN KEY (""ShoppingListSnapshotId"") REFERENCES ""ShoppingListSnapshots""(""Id"") ON DELETE SET NULL;
    END IF;
END$$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"ShoppingListSnapshots\" DROP CONSTRAINT IF EXISTS \"FK_ShoppingListSnapshots_MealPlanSnapshots_MealPlanSnapshotId\";");
            migrationBuilder.Sql("ALTER TABLE \"MealPlanSnapshots\" DROP CONSTRAINT IF EXISTS \"FK_MealPlanSnapshots_ShoppingListSnapshots_ShoppingListSnapshotId\";");

            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_ShoppingListSnapshots_MealPlanSnapshotId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_MealPlanSnapshots_ShoppingListSnapshotId\";");

            migrationBuilder.Sql("DROP TABLE IF EXISTS \"MealPlanSnapshots\";");
            migrationBuilder.Sql("ALTER TABLE \"ShoppingListSnapshots\" DROP COLUMN IF EXISTS \"MealPlanSnapshotId\";");
        }
    }
}
