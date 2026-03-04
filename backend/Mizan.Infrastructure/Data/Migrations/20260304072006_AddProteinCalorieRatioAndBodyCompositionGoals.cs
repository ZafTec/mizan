using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProteinCalorieRatioAndBodyCompositionGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "target_body_fat_percentage",
                table: "user_goals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "target_muscle_mass_kg",
                table: "user_goals",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "target_protein_calorie_ratio",
                table: "user_goals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "protein_calorie_ratio",
                table: "recipe_nutrition",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "protein_calorie_ratio",
                table: "foods",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "protein_calorie_ratio",
                table: "food_diary_entries",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            // Backfill existing rows
            migrationBuilder.Sql(@"UPDATE foods SET protein_calorie_ratio = CASE WHEN calories_per_100g > 0 THEN ROUND(protein_per_100g * 4.0 / calories_per_100g * 100.0, 2) ELSE 0 END WHERE protein_calorie_ratio = 0;");

            migrationBuilder.Sql(@"UPDATE recipe_nutrition SET protein_calorie_ratio = CASE WHEN calories_per_serving > 0 THEN ROUND(protein_grams * 4.0 / calories_per_serving * 100.0, 2) ELSE 0 END WHERE protein_calorie_ratio IS NULL;");

            migrationBuilder.Sql(@"UPDATE food_diary_entries SET protein_calorie_ratio = CASE WHEN calories > 0 THEN ROUND(protein_grams * 4.0 / calories * 100.0, 2) ELSE 0 END WHERE protein_calorie_ratio IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "target_body_fat_percentage",
                table: "user_goals");

            migrationBuilder.DropColumn(
                name: "target_muscle_mass_kg",
                table: "user_goals");

            migrationBuilder.DropColumn(
                name: "target_protein_calorie_ratio",
                table: "user_goals");

            migrationBuilder.DropColumn(
                name: "protein_calorie_ratio",
                table: "recipe_nutrition");

            migrationBuilder.DropColumn(
                name: "protein_calorie_ratio",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "protein_calorie_ratio",
                table: "food_diary_entries");
        }
    }
}
