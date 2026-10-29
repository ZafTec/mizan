using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificationMessagesAndSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "quiet_hours_end",
                table: "users",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "quiet_hours_start",
                table: "users",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actions",
                table: "notifications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "facts",
                table: "notifications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "notifications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "notifications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "schedule_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "card_image_url",
                table: "achievements",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    in_app = table.Column<bool>(type: "boolean", nullable: false),
                    telegram = table.Column<bool>(type: "boolean", nullable: false),
                    push = table.Column<bool>(type: "boolean", nullable: false),
                    email = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => new { x.user_id, x.kind });
                    table.ForeignKey(
                        name: "FK_notification_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    owner = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channels = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    skip_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    audience = table.Column<string>(type: "jsonb", nullable: false),
                    campaign = table.Column<string>(type: "jsonb", nullable: true),
                    message = table.Column<string>(type: "jsonb", nullable: false),
                    reminder = table.Column<string>(type: "jsonb", nullable: true),
                    trigger = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_schedules", x => x.id);
                    table.ForeignKey(
                        name: "FK_notification_schedules_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000001"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000002"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000003"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000004"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000005"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000006"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000007"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000008"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000009"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000010"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000011"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000012"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000013"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000014"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000015"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000016"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000017"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000018"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000019"),
                column: "card_image_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "achievements",
                keyColumn: "id",
                keyValue: new Guid("ac000000-0000-0000-0000-000000000020"),
                column: "card_image_url",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_schedule_id_read_at",
                table: "notifications",
                columns: new[] { "schedule_id", "read_at" },
                filter: "schedule_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notification_schedules_enabled_next_run_at",
                table: "notification_schedules",
                columns: new[] { "enabled", "next_run_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_schedules_key",
                table: "notification_schedules",
                column: "key",
                unique: true,
                filter: "key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notification_schedules_owner_user_id",
                table: "notification_schedules",
                column: "owner_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notification_schedules");

            migrationBuilder.DropIndex(
                name: "IX_notifications_schedule_id_read_at",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "quiet_hours_end",
                table: "users");

            migrationBuilder.DropColumn(
                name: "quiet_hours_start",
                table: "users");

            migrationBuilder.DropColumn(
                name: "actions",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "facts",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "schedule_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "card_image_url",
                table: "achievements");
        }
    }
}
