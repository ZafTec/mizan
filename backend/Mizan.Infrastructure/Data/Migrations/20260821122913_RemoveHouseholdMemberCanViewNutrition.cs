using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHouseholdMemberCanViewNutrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "can_view_nutrition",
                table: "household_members");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "can_view_nutrition",
                table: "household_members",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
