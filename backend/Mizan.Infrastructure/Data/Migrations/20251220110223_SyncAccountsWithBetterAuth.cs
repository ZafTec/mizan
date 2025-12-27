using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncAccountsWithBetterAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_accounts_provider_provider_account_id",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "provider_account_id",
                table: "accounts");

            migrationBuilder.RenameColumn(
                name: "expires_at",
                table: "accounts",
                newName: "refresh_token_expires_at");

            migrationBuilder.AddColumn<DateTime>(
                name: "access_token_expires_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "account_id",
                table: "accounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "id_token",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "password",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_id",
                table: "accounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "access_token_expires_at",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "account_id",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "id_token",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "password",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "provider_id",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "accounts");

            migrationBuilder.RenameColumn(
                name: "refresh_token_expires_at",
                table: "accounts",
                newName: "expires_at");

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "accounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "provider_account_id",
                table: "accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_provider_provider_account_id",
                table: "accounts",
                columns: new[] { "provider", "provider_account_id" },
                unique: true);
        }
    }
}
