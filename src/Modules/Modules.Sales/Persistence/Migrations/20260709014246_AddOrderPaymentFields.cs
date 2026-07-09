using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Sales.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cash_tendered",
                schema: "sales",
                table: "orders",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "change_due",
                schema: "sales",
                table: "orders",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                schema: "sales",
                table: "orders",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_percent",
                schema: "sales",
                table: "orders",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                schema: "sales",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "subtotal",
                schema: "sales",
                table: "orders",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_amount",
                schema: "sales",
                table: "orders",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate_percent",
                schema: "sales",
                table: "orders",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cash_tendered",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "change_due",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "discount_amount",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "discount_percent",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_method",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "subtotal",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "tax_amount",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "tax_rate_percent",
                schema: "sales",
                table: "orders");
        }
    }
}
