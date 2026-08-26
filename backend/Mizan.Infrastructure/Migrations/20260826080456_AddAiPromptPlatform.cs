using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Mizan.Infrastructure.Data.Seed;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiPromptPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "prompt_version_id",
                table: "ai_usage_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_eval_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    prompt_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    input = table.Column<string>(type: "text", nullable: false),
                    context = table.Column<string>(type: "text", nullable: true),
                    assertions = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    is_adversarial = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_eval_cases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_prompts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_prompts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_prompt_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    prompt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    soft_policy = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    status = table.Column<int>(type: "integer", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_prompt_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_prompt_versions_ai_prompts_prompt_id",
                        column: x => x.prompt_id,
                        principalTable: "ai_prompts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_eval_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<int>(type: "integer", nullable: false),
                    schema_valid = table.Column<bool>(type: "boolean", nullable: false),
                    output = table.Column<string>(type: "text", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    cost_micros = table.Column<long>(type: "bigint", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_eval_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_eval_runs_ai_eval_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "ai_eval_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_eval_runs_ai_prompt_versions_version_id",
                        column: x => x.version_id,
                        principalTable: "ai_prompt_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_eval_cases_prompt_key",
                table: "ai_eval_cases",
                column: "prompt_key");

            migrationBuilder.CreateIndex(
                name: "IX_ai_eval_runs_case_id",
                table: "ai_eval_runs",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_eval_runs_version_id",
                table: "ai_eval_runs",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_prompt_versions_one_published",
                table: "ai_prompt_versions",
                column: "prompt_id",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ai_prompt_versions_prompt_id_version",
                table: "ai_prompt_versions",
                columns: new[] { "prompt_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_prompts_key",
                table: "ai_prompts",
                column: "key",
                unique: true);

            // The synthetic suite ships with the schema: a fresh database can
            // gate its first publish without anyone hand-writing fixtures.
            migrationBuilder.Sql(AiEvalSeed.Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_eval_runs");

            migrationBuilder.DropTable(
                name: "ai_eval_cases");

            migrationBuilder.DropTable(
                name: "ai_prompt_versions");

            migrationBuilder.DropTable(
                name: "ai_prompts");

            migrationBuilder.DropColumn(
                name: "prompt_version_id",
                table: "ai_usage_logs");
        }
    }
}
