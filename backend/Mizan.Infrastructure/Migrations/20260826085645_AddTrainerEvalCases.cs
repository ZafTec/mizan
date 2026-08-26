using Microsoft.EntityFrameworkCore.Migrations;
using Mizan.Infrastructure.Data.Seed;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerEvalCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AiEvalSeed.Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ai_eval_cases WHERE prompt_key = 'trainer.client';");
        }
    }
}
