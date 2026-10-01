using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthAuthorizationServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Until now the audit log serialized whole commands, so a successful
            // sign-in, registration or password change stored the password in
            // audit_logs.details. Keep the event, drop the secrets.
            migrationBuilder.Sql(@"
                UPDATE audit_logs
                SET details = CASE action
                    WHEN 'LoginCommand' THEN json_build_object('Email', details::json->>'Email', 'IpAddress', details::json->>'IpAddress', 'UserAgent', details::json->>'UserAgent')::text
                    WHEN 'RegisterCommand' THEN json_build_object('Email', details::json->>'Email', 'Name', details::json->>'Name', 'TimeZoneId', details::json->>'TimeZoneId')::text
                    WHEN 'ResetPasswordCommand' THEN '{""Reset"":true}'
                    WHEN 'ChangePasswordCommand' THEN '{""Changed"":true}'
                    WHEN 'VerifyEmailCommand' THEN '{""Verified"":true}'
                    WHEN 'ConsumeTelegramLinkCommand' THEN json_build_object('TelegramUserId', details::json->>'TelegramUserId', 'TelegramUsername', details::json->>'TelegramUsername')::text
                    WHEN 'RequestFollowCommand' THEN '{""Requested"":true}'
                END
                WHERE action IN ('LoginCommand','RegisterCommand','ResetPasswordCommand','ChangePasswordCommand','VerifyEmailCommand','ConsumeTelegramLinkCommand','RequestFollowCommand')
                  AND details IS NOT NULL;
            ");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "oauth_authorization_requests");

            migrationBuilder.DropTable(
                name: "oauth_tokens");

            migrationBuilder.DropTable(
                name: "oauth_grants");

            migrationBuilder.DropTable(
                name: "oauth_clients");
        }
    }
}
