using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveToUserAndBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_ServiceOptions_ServiceId",
                table: "Bookings");

            // Add IsActive to Users with default true so existing accounts remain active
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<int>(
                name: "ServiceId",
                table: "Bookings",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Add IsActive to Bookings with default true so existing bookings remain active
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Recreate FK (ServiceId is nullable now)
            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_ServiceOptions_ServiceId",
                table: "Bookings",
                column: "ServiceId",
                principalTable: "ServiceOptions",
                principalColumn: "Id");

            // Safety: ensure any existing rows (if any tool created columns without default) are active
            migrationBuilder.Sql("UPDATE Users SET IsActive = 1 WHERE IsActive IS NULL");
            migrationBuilder.Sql("UPDATE Bookings SET IsActive = 1 WHERE IsActive IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_ServiceOptions_ServiceId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Bookings");

            migrationBuilder.AlterColumn<int>(
                name: "ServiceId",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_ServiceOptions_ServiceId",
                table: "Bookings",
                column: "ServiceId",
                principalTable: "ServiceOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}