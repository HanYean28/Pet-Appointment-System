using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.Migrations
{
    /// <inheritdoc />
    public partial class AddCountToServiceAndPackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Count",
                table: "ServiceOptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Count",
                table: "Packages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // NOTE: Do NOT add a Count column to Bookings here — it already exists.
            // If you intended to add Booking.Count, create a separate migration that
            // matches the model type (int) and ensure no duplicate migrations already did it.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Count",
                table: "ServiceOptions");

            migrationBuilder.DropColumn(
                name: "Count",
                table: "Packages");

            // Bookings column was not added by this migration.
        }
    }
}
