using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetFiberGramsToUserGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "target_fiber_grams",
                table: "user_goals",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "target_fiber_grams",
                table: "user_goals");
        }
    }
}
