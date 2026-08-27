using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiWriteConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_writes",
                table: "user_ai_consents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "write_body",
                table: "user_ai_consents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "write_nutrition",
                table: "user_ai_consents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "write_training",
                table: "user_ai_consents",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allow_writes",
                table: "user_ai_consents");

            migrationBuilder.DropColumn(
                name: "write_body",
                table: "user_ai_consents");

            migrationBuilder.DropColumn(
                name: "write_nutrition",
                table: "user_ai_consents");

            migrationBuilder.DropColumn(
                name: "write_training",
                table: "user_ai_consents");
        }
    }
}
