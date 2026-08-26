using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Mizan.Infrastructure.Data.Seed;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_chat_threads_user_id",
                table: "ai_chat_threads");

            migrationBuilder.DropColumn(
                name: "thread_data",
                table: "ai_chat_threads");

            migrationBuilder.DropColumn(
                name: "thread_type",
                table: "ai_chat_threads");

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "ai_chat_threads",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ai_chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    thread_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    prompt_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_chat_messages_ai_chat_threads_thread_id",
                        column: x => x.thread_id,
                        principalTable: "ai_chat_threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_threads_user_id_updated_at",
                table: "ai_chat_threads",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_messages_thread_id_created_at",
                table: "ai_chat_messages",
                columns: new[] { "thread_id", "created_at" });

            // Re-run of the fixture seed, which is ON CONFLICT DO NOTHING and
            // so backfills only the cases added since the last migration.
            migrationBuilder.Sql(AiEvalSeed.Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_ai_chat_threads_user_id_updated_at",
                table: "ai_chat_threads");

            migrationBuilder.DropColumn(
                name: "title",
                table: "ai_chat_threads");

            migrationBuilder.AddColumn<string>(
                name: "thread_data",
                table: "ai_chat_threads",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "thread_type",
                table: "ai_chat_threads",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "nutrition");

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_threads_user_id",
                table: "ai_chat_threads",
                column: "user_id");
        }
    }
}
