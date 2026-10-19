using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workouts_user_id",
                table: "workouts");

            migrationBuilder.DropIndex(
                name: "IX_body_measurements_user_id",
                table: "body_measurements");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "workouts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "food_diary_entries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "body_measurements",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            // Existing rows were last touched when they were written, not when this migration ran. Without
            // this an app syncing for the first time would see every old record as changed today.
            migrationBuilder.Sql("UPDATE workouts SET updated_at = created_at;");
            migrationBuilder.Sql("UPDATE food_diary_entries SET updated_at = logged_at;");
            migrationBuilder.Sql("UPDATE body_measurements SET updated_at = created_at;");

            migrationBuilder.CreateTable(
                name: "deleted_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deleted_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_deleted_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workouts_user_id_updated_at",
                table: "workouts",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_food_diary_entries_user_id_updated_at",
                table: "food_diary_entries",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_body_measurements_user_id_updated_at",
                table: "body_measurements",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_deleted_records_user_id_deleted_at",
                table: "deleted_records",
                columns: new[] { "user_id", "deleted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deleted_records");

            migrationBuilder.DropIndex(
                name: "IX_workouts_user_id_updated_at",
                table: "workouts");

            migrationBuilder.DropIndex(
                name: "IX_food_diary_entries_user_id_updated_at",
                table: "food_diary_entries");

            migrationBuilder.DropIndex(
                name: "IX_body_measurements_user_id_updated_at",
                table: "body_measurements");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "workouts");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "food_diary_entries");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "body_measurements");

            migrationBuilder.CreateIndex(
                name: "IX_workouts_user_id",
                table: "workouts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_body_measurements_user_id",
                table: "body_measurements",
                column: "user_id");
        }
    }
}
