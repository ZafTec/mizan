using Microsoft.EntityFrameworkCore.Migrations;
using Mizan.Infrastructure.Data.Seed;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingEvalCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Schema-only migrations carry no seed; this one is the reverse.
            // ON CONFLICT DO NOTHING means it adds the onboarding cases and
            // leaves every earlier one alone.
            migrationBuilder.Sql(AiEvalSeed.Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ai_eval_cases WHERE prompt_key = 'onboarding.agent';");
        }
    }
}
