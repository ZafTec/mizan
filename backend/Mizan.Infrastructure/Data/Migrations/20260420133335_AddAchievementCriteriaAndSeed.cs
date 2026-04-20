using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievementCriteriaAndSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "criteria_type",
                table: "achievements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "threshold",
                table: "achievements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropIndex(
                name: "IX_streaks_user_id",
                table: "streaks");

            migrationBuilder.CreateIndex(
                name: "IX_streaks_user_id_streak_type",
                table: "streaks",
                columns: new[] { "user_id", "streak_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_achievements_criteria_type_threshold",
                table: "achievements",
                columns: new[] { "criteria_type", "threshold" });

            migrationBuilder.InsertData(
                table: "achievements",
                columns: new[] { "id", "category", "criteria_type", "description", "icon_url", "name", "points", "threshold" },
                values: new object[,]
                {
                    { new Guid("ac000000-0000-0000-0000-000000000001"), "nutrition", "meals_logged", "Log your first meal", null, "First Meal", 10, 1 },
                    { new Guid("ac000000-0000-0000-0000-000000000002"), "nutrition", "meals_logged", "Log 10 meals", null, "Getting Started", 25, 10 },
                    { new Guid("ac000000-0000-0000-0000-000000000003"), "nutrition", "meals_logged", "Log 100 meals", null, "Century Club", 100, 100 },
                    { new Guid("ac000000-0000-0000-0000-000000000004"), "nutrition", "meals_logged", "Log 500 meals", null, "Half Grand", 250, 500 },
                    { new Guid("ac000000-0000-0000-0000-000000000005"), "consistency", "streak_nutrition", "Log meals 3 days in a row", null, "Three-Day Runner", 15, 3 },
                    { new Guid("ac000000-0000-0000-0000-000000000006"), "consistency", "streak_nutrition", "Log meals 7 days in a row", null, "One-Week Warrior", 50, 7 },
                    { new Guid("ac000000-0000-0000-0000-000000000007"), "consistency", "streak_nutrition", "Log meals 14 days in a row", null, "Two-Week Titan", 100, 14 },
                    { new Guid("ac000000-0000-0000-0000-000000000008"), "consistency", "streak_nutrition", "Log meals 30 days in a row", null, "Monthly Master", 250, 30 },
                    { new Guid("ac000000-0000-0000-0000-000000000009"), "consistency", "streak_nutrition", "Log meals 90 days in a row", null, "Quarter-Year Habit", 500, 90 },
                    { new Guid("ac000000-0000-0000-0000-000000000010"), "workout", "workouts_logged", "Log your first workout", null, "First Rep", 10, 1 },
                    { new Guid("ac000000-0000-0000-0000-000000000011"), "workout", "workouts_logged", "Log 10 workouts", null, "Gym Regular", 50, 10 },
                    { new Guid("ac000000-0000-0000-0000-000000000012"), "workout", "workouts_logged", "Log 50 workouts", null, "Iron Habit", 200, 50 },
                    { new Guid("ac000000-0000-0000-0000-000000000013"), "workout", "streak_workout", "Complete workouts 7 days in a row", null, "Workout Week", 75, 7 },
                    { new Guid("ac000000-0000-0000-0000-000000000014"), "milestone", "body_measurements_logged", "Record your first body measurement", null, "Baseline Set", 10, 1 },
                    { new Guid("ac000000-0000-0000-0000-000000000015"), "milestone", "body_measurements_logged", "Record 10 body measurements", null, "Body Tracker", 50, 10 },
                    { new Guid("ac000000-0000-0000-0000-000000000016"), "nutrition", "recipes_created", "Create your first recipe", null, "Kitchen Opener", 15, 1 },
                    { new Guid("ac000000-0000-0000-0000-000000000017"), "nutrition", "recipes_created", "Create 10 recipes", null, "Chef's Shelf", 75, 10 }
                });

            migrationBuilder.InsertData(
                table: "achievements",
                columns: new[] { "id", "category", "criteria_type", "description", "icon_url", "name", "threshold" },
                values: new object[,]
                {
                    { new Guid("ac000000-0000-0000-0000-000000000018"), "milestone", "points_total", "Earn 100 achievement points", null, "Bronze Badge", 100 },
                    { new Guid("ac000000-0000-0000-0000-000000000019"), "milestone", "points_total", "Earn 500 achievement points", null, "Silver Badge", 500 },
                    { new Guid("ac000000-0000-0000-0000-000000000020"), "milestone", "points_total", "Earn 1500 achievement points", null, "Gold Badge", 1500 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000020"));

            migrationBuilder.DropIndex(
                name: "IX_achievements_criteria_type_threshold",
                table: "achievements");

            migrationBuilder.DropIndex(
                name: "IX_streaks_user_id_streak_type",
                table: "streaks");

            migrationBuilder.CreateIndex(
                name: "IX_streaks_user_id",
                table: "streaks",
                column: "user_id");

            migrationBuilder.DropColumn(
                name: "criteria_type",
                table: "achievements");

            migrationBuilder.DropColumn(
                name: "threshold",
                table: "achievements");
        }
    }
}
