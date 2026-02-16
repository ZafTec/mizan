using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubRecipeIdToRecipeIngredient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "sub_recipe_id",
                table: "recipe_ingredients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_sub_recipe_id",
                table: "recipe_ingredients",
                column: "sub_recipe_id");

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_ingredients_recipes_sub_recipe_id",
                table: "recipe_ingredients",
                column: "sub_recipe_id",
                principalTable: "recipes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_recipe_ingredients_recipes_sub_recipe_id",
                table: "recipe_ingredients");

            migrationBuilder.DropIndex(
                name: "IX_recipe_ingredients_sub_recipe_id",
                table: "recipe_ingredients");

            migrationBuilder.DropColumn(
                name: "sub_recipe_id",
                table: "recipe_ingredients");
        }
    }
}
