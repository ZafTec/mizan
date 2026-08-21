using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodOwnershipAndRecipePreparations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_preparation",
                table: "recipes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "yield_grams",
                table: "recipes",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_recipe_id",
                table: "foods",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "foods",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_foods_source_recipe_id",
                table: "foods",
                column: "source_recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_foods_user_id",
                table: "foods",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_foods_recipes_source_recipe_id",
                table: "foods",
                column: "source_recipe_id",
                principalTable: "recipes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_foods_users_user_id",
                table: "foods",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_foods_recipes_source_recipe_id",
                table: "foods");

            migrationBuilder.DropForeignKey(
                name: "FK_foods_users_user_id",
                table: "foods");

            migrationBuilder.DropIndex(
                name: "IX_foods_source_recipe_id",
                table: "foods");

            migrationBuilder.DropIndex(
                name: "ix_foods_user_id",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "is_preparation",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "yield_grams",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "source_recipe_id",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "foods");
        }
    }
}
