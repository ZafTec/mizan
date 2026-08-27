using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatImagesAndSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "summarised_through",
                table: "ai_chat_threads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "summary",
                table: "ai_chat_threads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "ai_chat_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "summarised_through",
                table: "ai_chat_threads");

            migrationBuilder.DropColumn(
                name: "summary",
                table: "ai_chat_threads");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "ai_chat_messages");
        }
    }
}
