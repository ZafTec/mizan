using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BillingCatalogAndSelfService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "next_billed_at",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "paddle_updated_at",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scheduled_change_action",
                table: "subscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "scheduled_change_at",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "billing_discounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    recurring = table.Column<bool>(type: "boolean", nullable: false),
                    maximum_recurring_intervals = table.Column<int>(type: "integer", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    plan_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    paddle_discount_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_discounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "billing_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    interval = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    amount_cents = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    trial_days = table.Column<int>(type: "integer", nullable: true),
                    paddle_product_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    paddle_price_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_plans", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_discounts_code",
                table: "billing_discounts",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_billing_discounts_paddle_discount_id",
                table: "billing_discounts",
                column: "paddle_discount_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_plans_is_active_sort_order",
                table: "billing_plans",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_plans_paddle_price_id",
                table: "billing_plans",
                column: "paddle_price_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_discounts");

            migrationBuilder.DropTable(
                name: "billing_plans");

            migrationBuilder.DropColumn(
                name: "next_billed_at",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "paddle_updated_at",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "scheduled_change_action",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "scheduled_change_at",
                table: "subscriptions");
        }
    }
}
