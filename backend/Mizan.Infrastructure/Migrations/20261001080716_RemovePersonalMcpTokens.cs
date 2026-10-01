using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovePersonalMcpTokens : Migration
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
                name: "IX_mcp_usage_logs_mcp_token_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropColumn(
                name: "mcp_token_id",
                table: "mcp_usage_logs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "mcp_token_id",
                table: "mcp_usage_logs",
                type: "uuid",
                nullable: true);

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
                name: "IX_mcp_usage_logs_mcp_token_id",
                table: "mcp_usage_logs",
                column: "mcp_token_id");

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
