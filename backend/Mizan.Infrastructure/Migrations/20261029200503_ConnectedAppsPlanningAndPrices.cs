using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConnectedAppsPlanningAndPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            BranchDataMotion.Before(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_mcp_usage_logs_mcp_tokens_mcp_token_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropTable(
                name: "mcp_tokens");

            migrationBuilder.DropTable(
                name: "meal_plan_recipes");

            migrationBuilder.DropIndex(
                name: "IX_workouts_user_id",
                table: "workouts");

            migrationBuilder.DropIndex(
                name: "IX_shopping_lists_user_id",
                table: "shopping_lists");

            migrationBuilder.DropIndex(
                name: "IX_meal_plans_user_id",
                table: "meal_plans");

            migrationBuilder.DropIndex(
                name: "IX_mcp_usage_logs_mcp_token_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropIndex(
                name: "IX_body_measurements_user_id",
                table: "body_measurements");

            migrationBuilder.DropColumn(
                name: "end_date",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "mcp_token_id",
                table: "mcp_usage_logs");

            migrationBuilder.RenameIndex(
                name: "IX_shopping_list_items_shopping_list_id",
                table: "shopping_list_items",
                newName: "ix_shopping_list_items_list");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "workouts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<bool>(
                name: "notify_by_telegram",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "price_region_code",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

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

            migrationBuilder.AddColumn<Guid>(
                name: "impersonator_id",
                table: "user_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "can_plan_meals",
                table: "trainer_client_relationships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "generated_at",
                table: "shopping_lists",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "meal_plan_id",
                table: "shopping_lists",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "grams",
                table: "shopping_list_items",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "needs_review",
                table: "shopping_list_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "prep_session_id",
                table: "shopping_list_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "shopping_list_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "meal_plans",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "start_date",
                table: "meal_plans",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<Guid>(
                name: "authored_by_user_id",
                table: "meal_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "meal_plans",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duration_days",
                table: "meal_plans",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<string>(
                name: "generated_from",
                table: "meal_plans",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "meal_plans",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Personal");

            migrationBuilder.AddColumn<string>(
                name: "rules",
                table: "meal_plans",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "meal_plans",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<Guid>(
                name: "grant_id",
                table: "mcp_usage_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "mcp_usage_logs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "tool");

            migrationBuilder.AddColumn<Guid>(
                name: "plan_entry_id",
                table: "food_diary_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "food_diary_entries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "model_url",
                table: "exercises",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "body_measurements",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<Guid>(
                name: "impersonator_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_context",
                table: "ai_chat_threads",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "pending_context_until",
                table: "ai_chat_threads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "card_image_url",
                table: "achievements",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    audience = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recipient_count = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contact_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    message = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    handled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    handled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reply_email_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_submissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_protection_keys", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "device_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    token = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    device_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    outbox_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feature_flag_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    flag_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_flag_overrides", x => x.id);
                    table.ForeignKey(
                        name: "FK_feature_flag_overrides_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_flags",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    default_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_flags", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    response_body = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => new { x.user_id, x.key });
                    table.ForeignKey(
                        name: "FK_idempotency_keys_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mcp_tasks",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    result_json = table.Column<string>(type: "jsonb", nullable: true),
                    error_json = table.Column<string>(type: "jsonb", nullable: true),
                    ttl_seconds = table.Column<int>(type: "integer", nullable: false),
                    poll_interval_ms = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_mcp_tasks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "oauth_clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    client_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    logo_uri = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    client_uri = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    redirect_uris = table.Column<List<string>>(type: "text[]", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    verified_host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    metadata_fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "prep_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    meal_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_index = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prep_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_prep_sessions_meal_plans_meal_plan_id",
                        column: x => x.meal_plan_id,
                        principalTable: "meal_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_regions",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_regions", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "support_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_notes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    is_secret = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.key);
                    table.ForeignKey(
                        name: "FK_system_settings_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "usage_limit_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    daily_requests = table.Column<int>(type: "integer", nullable: true),
                    daily_tokens = table.Column<long>(type: "bigint", nullable: true),
                    daily_cost_micros = table.Column<long>(type: "bigint", nullable: true),
                    monthly_cost_micros = table.Column<long>(type: "bigint", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_limit_overrides", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "oauth_authorization_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    redirect_uri = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    requested_scopes = table.Column<List<string>>(type: "text[]", nullable: false),
                    state = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    code_challenge = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    audience = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_authorization_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_oauth_authorization_requests_oauth_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "oauth_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "oauth_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scopes = table.Column<List<string>>(type: "text[]", nullable: false),
                    household_mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    household_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_grants", x => x.id);
                    table.ForeignKey(
                        name: "FK_oauth_grants_oauth_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "oauth_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_oauth_grants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    meal_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_index = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    servings = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    food_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grams = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    text = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    prep_session_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_plan_entries_foods_food_id",
                        column: x => x.food_id,
                        principalTable: "foods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_entries_meal_plans_meal_plan_id",
                        column: x => x.meal_plan_id,
                        principalTable: "meal_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_plan_entries_prep_sessions_prep_session_id",
                        column: x => x.prep_session_id,
                        principalTable: "prep_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_plan_entries_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    food_id = table.Column<Guid>(type: "uuid", nullable: false),
                    region_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    suggested_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false, defaultValue: 1m),
                    unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    shown_price_per_gram = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_price_reports_foods_food_id",
                        column: x => x.food_id,
                        principalTable: "foods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_price_reports_price_regions_region_code",
                        column: x => x.region_code,
                        principalTable: "price_regions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_price_reports_users_resolved_by_user_id",
                        column: x => x.resolved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_price_reports_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    scraper_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    region_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    schedule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    last_observations = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_sources", x => x.id);
                    table.ForeignKey(
                        name: "FK_price_sources_price_regions_region_code",
                        column: x => x.region_code,
                        principalTable: "price_regions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "oauth_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    audience = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_oauth_tokens_oauth_grants_grant_id",
                        column: x => x.grant_id,
                        principalTable: "oauth_grants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "food_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    food_id = table.Column<Guid>(type: "uuid", nullable: false),
                    region_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false, defaultValue: 1m),
                    unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    confidence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_prices", x => x.id);
                    table.ForeignKey(
                        name: "FK_food_prices_foods_food_id",
                        column: x => x.food_id,
                        principalTable: "foods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_food_prices_price_regions_region_code",
                        column: x => x.region_code,
                        principalTable: "price_regions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_food_prices_price_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "price_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "price_source_aliases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    product_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    food_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ignored = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_seen_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_seen_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_source_aliases", x => x.id);
                    table.ForeignKey(
                        name: "FK_price_source_aliases_foods_food_id",
                        column: x => x.food_id,
                        principalTable: "foods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_price_source_aliases_price_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "price_sources",
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

            migrationBuilder.InsertData(
                table: "price_regions",
                columns: new[] { "code", "created_at", "currency", "enabled", "name" },
                values: new object[] { "ET-AA", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "ETB", true, "Addis Ababa" });

            migrationBuilder.CreateIndex(
                name: "IX_workouts_user_id_updated_at",
                table: "workouts",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_impersonator_id",
                table: "user_sessions",
                column: "impersonator_id");

            migrationBuilder.CreateIndex(
                name: "IX_shopping_lists_meal_plan_id",
                table: "shopping_lists",
                column: "meal_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_shopping_lists_user_updated",
                table: "shopping_lists",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_schedule_id_read_at",
                table: "notifications",
                columns: new[] { "schedule_id", "read_at" },
                filter: "schedule_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plans_authored_by_user_id",
                table: "meal_plans",
                column: "authored_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_meal_plans_kind_user",
                table: "meal_plans",
                columns: new[] { "kind", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_meal_plans_user_status_start",
                table: "meal_plans",
                columns: new[] { "user_id", "status", "start_date" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_usage_logs_grant_id",
                table: "mcp_usage_logs",
                column: "grant_id");

            migrationBuilder.CreateIndex(
                name: "IX_food_diary_entries_plan_entry_id",
                table: "food_diary_entries",
                column: "plan_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_food_diary_entries_user_id_updated_at",
                table: "food_diary_entries",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_body_measurements_user_id_updated_at",
                table: "body_measurements",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_announcements_audience_created_at",
                table: "announcements",
                columns: new[] { "audience", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_submissions_ip_hash_created_at",
                table: "contact_submissions",
                columns: new[] { "ip_hash", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_submissions_status_created_at",
                table: "contact_submissions",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_deleted_records_user_id_deleted_at",
                table: "deleted_records",
                columns: new[] { "user_id", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_device_tokens_token",
                table: "device_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_tokens_user_id",
                table: "device_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_created_at",
                table: "email_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_kind_created_at",
                table: "email_messages",
                columns: new[] { "kind", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_outbox_job_id",
                table: "email_messages",
                column: "outbox_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_status_created_at",
                table: "email_messages",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_to_user_id",
                table: "email_messages",
                column: "to_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_flag_overrides_flag_key_household_id",
                table: "feature_flag_overrides",
                columns: new[] { "flag_key", "household_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feature_flag_overrides_household_id",
                table: "feature_flag_overrides",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_food_prices_food_region_observed",
                table: "food_prices",
                columns: new[] { "food_id", "region_code", "observed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_food_prices_region_code",
                table: "food_prices",
                column: "region_code");

            migrationBuilder.CreateIndex(
                name: "IX_food_prices_source_id",
                table: "food_prices",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_tasks_user_id_created_at",
                table: "mcp_tasks",
                columns: new[] { "user_id", "created_at" });

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

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_requests_client_id",
                table: "oauth_authorization_requests",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_requests_code_hash",
                table: "oauth_authorization_requests",
                column: "code_hash",
                unique: true,
                filter: "code_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_requests_expires_at",
                table: "oauth_authorization_requests",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_requests_request_hash",
                table: "oauth_authorization_requests",
                column: "request_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_oauth_clients_client_id",
                table: "oauth_clients",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_oauth_grants_client_id",
                table: "oauth_grants",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_grants_user_id_client_id",
                table: "oauth_grants",
                columns: new[] { "user_id", "client_id" },
                unique: true,
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_tokens_family_id",
                table: "oauth_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_tokens_grant_id",
                table: "oauth_tokens",
                column: "grant_id");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_tokens_token_hash",
                table: "oauth_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_entries_food_id",
                table: "plan_entries",
                column: "food_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_entries_plan_day",
                table: "plan_entries",
                columns: new[] { "meal_plan_id", "day_index" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_entries_prep_session_id",
                table: "plan_entries",
                column: "prep_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_plan_entries_recipe_id",
                table: "plan_entries",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_prep_sessions_meal_plan",
                table: "prep_sessions",
                column: "meal_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_price_reports_food_id",
                table: "price_reports",
                column: "food_id");

            migrationBuilder.CreateIndex(
                name: "IX_price_reports_region_code",
                table: "price_reports",
                column: "region_code");

            migrationBuilder.CreateIndex(
                name: "IX_price_reports_resolved_by_user_id",
                table: "price_reports",
                column: "resolved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_reports_status_created",
                table: "price_reports",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_price_reports_user_id",
                table: "price_reports",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_source_aliases_food",
                table: "price_source_aliases",
                column: "food_id");

            migrationBuilder.CreateIndex(
                name: "ux_price_source_aliases_source_product",
                table: "price_source_aliases",
                columns: new[] { "source_id", "product_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_price_sources_region_name",
                table: "price_sources",
                columns: new[] { "region_code", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_support_notes_subject_type_subject_id_created_at",
                table: "support_notes",
                columns: new[] { "subject_type", "subject_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_updated_by_user_id",
                table: "system_settings",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_usage_limit_overrides_scope_subject_id_line",
                table: "usage_limit_overrides",
                columns: new[] { "scope", "subject_id", "line" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "FK_food_diary_entries_plan_entries_plan_entry_id",
                table: "food_diary_entries",
                column: "plan_entry_id",
                principalTable: "plan_entries",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_mcp_usage_logs_oauth_grants_grant_id",
                table: "mcp_usage_logs",
                column: "grant_id",
                principalTable: "oauth_grants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_meal_plans_users_authored_by_user_id",
                table: "meal_plans",
                column: "authored_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_shopping_lists_meal_plans_meal_plan_id",
                table: "shopping_lists",
                column: "meal_plan_id",
                principalTable: "meal_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            BranchDataMotion.After(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            BranchDataMotion.Down(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_food_diary_entries_plan_entries_plan_entry_id",
                table: "food_diary_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_mcp_usage_logs_oauth_grants_grant_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_meal_plans_users_authored_by_user_id",
                table: "meal_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_shopping_lists_meal_plans_meal_plan_id",
                table: "shopping_lists");

            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "contact_submissions");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "deleted_records");

            migrationBuilder.DropTable(
                name: "device_tokens");

            migrationBuilder.DropTable(
                name: "email_messages");

            migrationBuilder.DropTable(
                name: "feature_flag_overrides");

            migrationBuilder.DropTable(
                name: "feature_flags");

            migrationBuilder.DropTable(
                name: "food_prices");

            migrationBuilder.DropTable(
                name: "idempotency_keys");

            migrationBuilder.DropTable(
                name: "mcp_tasks");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notification_schedules");

            migrationBuilder.DropTable(
                name: "oauth_authorization_requests");

            migrationBuilder.DropTable(
                name: "oauth_tokens");

            migrationBuilder.DropTable(
                name: "plan_entries");

            migrationBuilder.DropTable(
                name: "price_reports");

            migrationBuilder.DropTable(
                name: "price_source_aliases");

            migrationBuilder.DropTable(
                name: "support_notes");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "usage_limit_overrides");

            migrationBuilder.DropTable(
                name: "oauth_grants");

            migrationBuilder.DropTable(
                name: "prep_sessions");

            migrationBuilder.DropTable(
                name: "price_sources");

            migrationBuilder.DropTable(
                name: "oauth_clients");

            migrationBuilder.DropTable(
                name: "price_regions");

            migrationBuilder.DropIndex(
                name: "IX_workouts_user_id_updated_at",
                table: "workouts");

            migrationBuilder.DropIndex(
                name: "IX_user_sessions_impersonator_id",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_shopping_lists_meal_plan_id",
                table: "shopping_lists");

            migrationBuilder.DropIndex(
                name: "ix_shopping_lists_user_updated",
                table: "shopping_lists");

            migrationBuilder.DropIndex(
                name: "IX_notifications_schedule_id_read_at",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_meal_plans_authored_by_user_id",
                table: "meal_plans");

            migrationBuilder.DropIndex(
                name: "ix_meal_plans_kind_user",
                table: "meal_plans");

            migrationBuilder.DropIndex(
                name: "ix_meal_plans_user_status_start",
                table: "meal_plans");

            migrationBuilder.DropIndex(
                name: "IX_mcp_usage_logs_grant_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropIndex(
                name: "IX_food_diary_entries_plan_entry_id",
                table: "food_diary_entries");

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
                name: "notify_by_telegram",
                table: "users");

            migrationBuilder.DropColumn(
                name: "price_region_code",
                table: "users");

            migrationBuilder.DropColumn(
                name: "quiet_hours_end",
                table: "users");

            migrationBuilder.DropColumn(
                name: "quiet_hours_start",
                table: "users");

            migrationBuilder.DropColumn(
                name: "impersonator_id",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "can_plan_meals",
                table: "trainer_client_relationships");

            migrationBuilder.DropColumn(
                name: "generated_at",
                table: "shopping_lists");

            migrationBuilder.DropColumn(
                name: "meal_plan_id",
                table: "shopping_lists");

            migrationBuilder.DropColumn(
                name: "grams",
                table: "shopping_list_items");

            migrationBuilder.DropColumn(
                name: "needs_review",
                table: "shopping_list_items");

            migrationBuilder.DropColumn(
                name: "prep_session_id",
                table: "shopping_list_items");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "shopping_list_items");

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
                name: "authored_by_user_id",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "description",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "duration_days",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "generated_from",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "rules",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "status",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "grant_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "mcp_usage_logs");

            migrationBuilder.DropColumn(
                name: "plan_entry_id",
                table: "food_diary_entries");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "food_diary_entries");

            migrationBuilder.DropColumn(
                name: "model_url",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "body_measurements");

            migrationBuilder.DropColumn(
                name: "impersonator_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "pending_context",
                table: "ai_chat_threads");

            migrationBuilder.DropColumn(
                name: "pending_context_until",
                table: "ai_chat_threads");

            migrationBuilder.DropColumn(
                name: "card_image_url",
                table: "achievements");

            migrationBuilder.RenameIndex(
                name: "ix_shopping_list_items_list",
                table: "shopping_list_items",
                newName: "IX_shopping_list_items_shopping_list_id");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "meal_plans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "start_date",
                table: "meal_plans",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "end_date",
                table: "meal_plans",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<Guid>(
                name: "mcp_token_id",
                table: "mcp_usage_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "mcp_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_mcp_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_plan_recipes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    meal_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    meal_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    servings = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 1m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_recipes", x => x.id);
                    table.ForeignKey(
                        name: "FK_meal_plan_recipes_meal_plans_meal_plan_id",
                        column: x => x.meal_plan_id,
                        principalTable: "meal_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meal_plan_recipes_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workouts_user_id",
                table: "workouts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shopping_lists_user_id",
                table: "shopping_lists",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plans_user_id",
                table: "meal_plans",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_usage_logs_mcp_token_id",
                table: "mcp_usage_logs",
                column: "mcp_token_id");

            migrationBuilder.CreateIndex(
                name: "IX_body_measurements_user_id",
                table: "body_measurements",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_tokens_token_hash",
                table: "mcp_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mcp_tokens_user_id_is_active",
                table: "mcp_tokens",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_recipes_meal_plan_id",
                table: "meal_plan_recipes",
                column: "meal_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_recipes_recipe_id",
                table: "meal_plan_recipes",
                column: "recipe_id");

            migrationBuilder.AddForeignKey(
                name: "FK_mcp_usage_logs_mcp_tokens_mcp_token_id",
                table: "mcp_usage_logs",
                column: "mcp_token_id",
                principalTable: "mcp_tokens",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
