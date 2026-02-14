using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUserGoalToOneToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_goals_user_id",
                table: "user_goals");

            migrationBuilder.CreateIndex(
                name: "IX_user_goals_user_id",
                table: "user_goals",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_goals_user_id",
                table: "user_goals");

            migrationBuilder.CreateIndex(
                name: "IX_user_goals_user_id",
                table: "user_goals",
                column: "user_id",
                unique: true);
        }
    }
}
