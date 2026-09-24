using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecipeNutritionSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recipe_nutrition_snapshots",
                columns: table => new
                {
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calories = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    protein_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    carbs_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    fat_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    fiber_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    servings = table.Column<int>(type: "integer", nullable: false),
                    ingredients_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_nutrition_snapshots", x => x.recipe_id);
                    table.ForeignKey(
                        name: "FK_recipe_nutrition_snapshots_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recipe_nutrition_snapshots");
        }
    }
}
