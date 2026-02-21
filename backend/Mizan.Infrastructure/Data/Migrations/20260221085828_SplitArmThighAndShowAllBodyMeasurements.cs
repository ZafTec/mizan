using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitArmThighAndShowAllBodyMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "thighs_cm",
                table: "body_measurements",
                newName: "right_thigh_cm");

            migrationBuilder.RenameColumn(
                name: "arms_cm",
                table: "body_measurements",
                newName: "right_arm_cm");

            migrationBuilder.AddColumn<decimal>(
                name: "left_arm_cm",
                table: "body_measurements",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "left_thigh_cm",
                table: "body_measurements",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "left_arm_cm",
                table: "body_measurements");

            migrationBuilder.DropColumn(
                name: "left_thigh_cm",
                table: "body_measurements");

            migrationBuilder.RenameColumn(
                name: "right_thigh_cm",
                table: "body_measurements",
                newName: "thighs_cm");

            migrationBuilder.RenameColumn(
                name: "right_arm_cm",
                table: "body_measurements",
                newName: "arms_cm");
        }
    }
}
