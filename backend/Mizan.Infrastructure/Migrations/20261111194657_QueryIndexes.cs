using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations;

/// <inheritdoc />
public partial class _20261111194657_QueryIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_outbox_jobs_dedupe",
            table: "outbox_jobs");

        migrationBuilder.DropIndex(
            name: "IX_food_diary_entries_food_id",
            table: "food_diary_entries");

        migrationBuilder.DropIndex(
            name: "IX_food_diary_entries_recipe_id",
            table: "food_diary_entries");

        migrationBuilder.DropIndex(
            name: "IX_chat_messages_conversation_id",
            table: "chat_messages");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

        migrationBuilder.CreateIndex(
            name: "ix_outbox_jobs_dedupe",
            table: "outbox_jobs",
            column: "dedupe_key",
            unique: true,
            filter: "dedupe_key IS NOT NULL")
            .Annotation("Npgsql:IndexOperators", new[] { "text_pattern_ops" });

        migrationBuilder.CreateIndex(
            name: "ix_outbox_jobs_retention",
            table: "outbox_jobs",
            columns: new[] { "status", "completed_at" });

        migrationBuilder.CreateIndex(
            name: "ix_foods_brand_trgm",
            table: "foods",
            column: "brand")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        migrationBuilder.CreateIndex(
            name: "ix_foods_name_trgm",
            table: "foods",
            column: "name")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        migrationBuilder.CreateIndex(
            name: "ix_food_diary_entries_food_user_logged",
            table: "food_diary_entries",
            columns: new[] { "food_id", "user_id", "logged_at" });

        migrationBuilder.CreateIndex(
            name: "ix_food_diary_entries_recipe_user_logged",
            table: "food_diary_entries",
            columns: new[] { "recipe_id", "user_id", "logged_at" });

        migrationBuilder.CreateIndex(
            name: "ix_chat_messages_conversation_sent",
            table: "chat_messages",
            columns: new[] { "conversation_id", "sent_at", "id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_outbox_jobs_dedupe",
            table: "outbox_jobs");

        migrationBuilder.DropIndex(
            name: "ix_outbox_jobs_retention",
            table: "outbox_jobs");

        migrationBuilder.DropIndex(
            name: "ix_foods_brand_trgm",
            table: "foods");

        migrationBuilder.DropIndex(
            name: "ix_foods_name_trgm",
            table: "foods");

        migrationBuilder.DropIndex(
            name: "ix_food_diary_entries_food_user_logged",
            table: "food_diary_entries");

        migrationBuilder.DropIndex(
            name: "ix_food_diary_entries_recipe_user_logged",
            table: "food_diary_entries");

        migrationBuilder.DropIndex(
            name: "ix_chat_messages_conversation_sent",
            table: "chat_messages");

        migrationBuilder.AlterDatabase()
            .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

        migrationBuilder.CreateIndex(
            name: "ix_outbox_jobs_dedupe",
            table: "outbox_jobs",
            column: "dedupe_key",
            unique: true,
            filter: "dedupe_key IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_food_diary_entries_food_id",
            table: "food_diary_entries",
            column: "food_id");

        migrationBuilder.CreateIndex(
            name: "IX_food_diary_entries_recipe_id",
            table: "food_diary_entries",
            column: "recipe_id");

        migrationBuilder.CreateIndex(
            name: "IX_chat_messages_conversation_id",
            table: "chat_messages",
            column: "conversation_id");
    }
}
