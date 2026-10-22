using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConnectedAppsNativeImpersonationAndBadges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_mcp_usage_logs_mcp_tokens_mcp_token_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropTable(
                name: "mcp_tokens");

            migrationBuilder.DropIndex(
                name: "IX_workouts_user_id",
                table: "workouts");

            migrationBuilder.DropIndex(
                name: "IX_mcp_usage_logs_mcp_token_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropIndex(
                name: "IX_body_measurements_user_id",
                table: "body_measurements");

            migrationBuilder.DropColumn(
                name: "mcp_token_id",
                table: "mcp_usage_logs");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "workouts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<Guid>(
                name: "impersonator_id",
                table: "user_sessions",
                type: "uuid",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_workouts_user_id_updated_at",
                table: "workouts",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_impersonator_id",
                table: "user_sessions",
                column: "impersonator_id");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_usage_logs_grant_id",
                table: "mcp_usage_logs",
                column: "grant_id");

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
                name: "IX_mcp_tasks_user_id_created_at",
                table: "mcp_tasks",
                columns: new[] { "user_id", "created_at" });

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

            migrationBuilder.AddForeignKey(
                name: "FK_mcp_usage_logs_oauth_grants_grant_id",
                table: "mcp_usage_logs",
                column: "grant_id",
                principalTable: "oauth_grants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            BranchDataMotion.Up(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            BranchDataMotion.Down(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_mcp_usage_logs_oauth_grants_grant_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropTable(
                name: "deleted_records");

            migrationBuilder.DropTable(
                name: "device_tokens");

            migrationBuilder.DropTable(
                name: "idempotency_keys");

            migrationBuilder.DropTable(
                name: "mcp_tasks");

            migrationBuilder.DropTable(
                name: "oauth_authorization_requests");

            migrationBuilder.DropTable(
                name: "oauth_tokens");

            migrationBuilder.DropTable(
                name: "oauth_grants");

            migrationBuilder.DropTable(
                name: "oauth_clients");

            migrationBuilder.DropIndex(
                name: "IX_workouts_user_id_updated_at",
                table: "workouts");

            migrationBuilder.DropIndex(
                name: "IX_user_sessions_impersonator_id",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_mcp_usage_logs_grant_id",
                table: "mcp_usage_logs");

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
                name: "impersonator_id",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "grant_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "mcp_usage_logs");

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

            migrationBuilder.CreateIndex(
                name: "IX_workouts_user_id",
                table: "workouts",
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
