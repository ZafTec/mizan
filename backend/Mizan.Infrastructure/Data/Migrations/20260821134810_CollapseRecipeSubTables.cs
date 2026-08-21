using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CollapseRecipeSubTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ORDER MATTERS. The scaffolder put DropTable("recipe_instructions")
            // before the column that replaces it even existed, which would have
            // discarded every instruction in the database. Add the column, fold
            // the rows into it, and only then drop.

            migrationBuilder.AddColumn<string>(
                name: "instructions",
                table: "recipes",
                type: "text",
                nullable: true);

            // Fold recipe_instructions into recipes.instructions as a numbered
            // list in step order. string_agg over an ordered set keeps the steps
            // in sequence; recipes with no instructions are left NULL rather than
            // set to an empty string, so "none recorded" stays distinguishable.
            migrationBuilder.Sql(@"
                UPDATE recipes r
                SET instructions = agg.body
                FROM (
                    SELECT recipe_id,
                           string_agg(step_number || '. ' || instruction, E'\n'
                                      ORDER BY step_number) AS body
                    FROM recipe_instructions
                    GROUP BY recipe_id
                ) AS agg
                WHERE r.id = agg.recipe_id;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_ingredients_recipes_sub_recipe_id",
                table: "recipe_ingredients");

            migrationBuilder.DropIndex(
                name: "IX_recipe_ingredients_sub_recipe_id",
                table: "recipe_ingredients");

            // Verified against production before writing this: zero rows carry a
            // sub_recipe_id, so nothing needs converting to a preparation first.
            migrationBuilder.DropColumn(
                name: "sub_recipe_id",
                table: "recipe_ingredients");

            migrationBuilder.DropTable(
                name: "recipe_instructions");

            // recipe_nutrition is not folded anywhere: totals are summed from the
            // ingredients on read now, so any stored value is either reproducible
            // or was already wrong.
            migrationBuilder.DropTable(
                name: "recipe_nutrition");

            migrationBuilder.DropTable(
                name: "recipe_tags");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The tables are recreated below; the instructions text is split back
            // into rows at the end, once recipe_instructions exists again.

            migrationBuilder.AddColumn<Guid>(
                name: "sub_recipe_id",
                table: "recipe_ingredients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recipe_instructions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instruction = table.Column<string>(type: "text", nullable: false),
                    step_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_instructions", x => x.id);
                    table.ForeignKey(
                        name: "FK_recipe_instructions_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_nutrition",
                columns: table => new
                {
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calories_per_serving = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    carbs_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    fat_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    fiber_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    protein_calorie_ratio = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    protein_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    sodium_mg = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    sugar_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_nutrition", x => x.recipe_id);
                    table.ForeignKey(
                        name: "FK_recipe_nutrition_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_tags", x => x.id);
                    table.ForeignKey(
                        name: "FK_recipe_tags_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_sub_recipe_id",
                table: "recipe_ingredients",
                column: "sub_recipe_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_instructions_recipe_id",
                table: "recipe_instructions",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_tags_recipe_id",
                table: "recipe_tags",
                column: "recipe_id");

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_ingredients_recipes_sub_recipe_id",
                table: "recipe_ingredients",
                column: "sub_recipe_id",
                principalTable: "recipes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Split recipes.instructions back into numbered rows. Lines that do
            // not carry a leading "N. " are kept whole and numbered by position.
            migrationBuilder.Sql(@"
                INSERT INTO recipe_instructions (id, recipe_id, step_number, instruction)
                SELECT gen_random_uuid(),
                       r.id,
                       s.ord,
                       regexp_replace(s.line, '^[0-9]+\. ', '')
                FROM recipes r
                CROSS JOIN LATERAL unnest(string_to_array(r.instructions, E'\n'))
                     WITH ORDINALITY AS s(line, ord)
                WHERE r.instructions IS NOT NULL AND btrim(s.line) <> '';
            ");

            migrationBuilder.DropColumn(
                name: "instructions",
                table: "recipes");
        }
    }
}
