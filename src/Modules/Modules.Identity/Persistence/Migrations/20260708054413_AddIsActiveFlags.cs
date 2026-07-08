using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "identity",
                table: "brands",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "identity",
                table: "branches",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "identity",
                table: "brands");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "identity",
                table: "branches");
        }
    }
}
