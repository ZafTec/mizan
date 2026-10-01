using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class McpUsageForConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "mcp_token_id",
                table: "mcp_usage_logs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

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

            migrationBuilder.CreateIndex(
                name: "IX_mcp_usage_logs_grant_id",
                table: "mcp_usage_logs",
                column: "grant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_mcp_usage_logs_oauth_grants_grant_id",
                table: "mcp_usage_logs",
                column: "grant_id",
                principalTable: "oauth_grants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_mcp_usage_logs_oauth_grants_grant_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropIndex(
                name: "IX_mcp_usage_logs_grant_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropColumn(
                name: "grant_id",
                table: "mcp_usage_logs");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "mcp_usage_logs");

            migrationBuilder.AlterColumn<Guid>(
                name: "mcp_token_id",
                table: "mcp_usage_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
