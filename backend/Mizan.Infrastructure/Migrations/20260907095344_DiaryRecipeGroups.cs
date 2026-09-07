using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DiaryRecipeGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "amount_grams",
                table: "food_diary_entries",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "food_diary_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_name",
                table: "food_diary_entries",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "amount_grams",
                table: "food_diary_entries");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "food_diary_entries");

            migrationBuilder.DropColumn(
                name: "group_name",
                table: "food_diary_entries");
        }
    }
}
