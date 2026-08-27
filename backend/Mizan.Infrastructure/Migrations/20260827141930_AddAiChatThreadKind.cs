using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiChatThreadKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "kind",
                table: "ai_chat_threads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_threads_user_id_kind",
                table: "ai_chat_threads",
                columns: new[] { "user_id", "kind" });

            // Setup threads predating this column are indistinguishable from
            // chat except by the title the handler gave them. Claiming them now
            // is the difference between a half-finished setup resuming and it
            // sitting orphaned in the chat list forever.
            migrationBuilder.Sql(
                "UPDATE ai_chat_threads SET kind = 1 WHERE title = 'Getting set up';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_chat_threads_user_id_kind",
                table: "ai_chat_threads");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "ai_chat_threads");
        }
    }
}
