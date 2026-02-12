using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpUsageLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mcp_usage_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    mcp_token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tool_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    execution_time_ms = table.Column<int>(type: "integer", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_usage_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_mcp_usage_logs_mcp_tokens_mcp_token_id",
                        column: x => x.mcp_token_id,
                        principalTable: "mcp_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mcp_usage_logs_users_user_id",
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
                name: "IX_mcp_usage_logs_tool_name",
                table: "mcp_usage_logs",
                column: "tool_name");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_usage_logs_user_id_timestamp",
                table: "mcp_usage_logs",
                columns: new[] { "user_id", "timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mcp_usage_logs");
        }
    }
}
