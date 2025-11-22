using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.Migrations
{
    /// <inheritdoc />
    public partial class plsss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Payment_PaymentId",
                table: "Bookings");

            //migrationBuilder.DropTable(
            //    name: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Payment_BookingId",
                table: "Payment");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_PaymentId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "Bookings");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_BookingId",
                table: "Payment",
                column: "BookingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payment_BookingId",
                table: "Payment");

            //migrationBuilder.DropColumn(
            //    name: "Status",
            //    table: "Bookings");

            migrationBuilder.AddColumn<int>(
                name: "PaymentId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payment_BookingId",
                table: "Payment",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PaymentId",
                table: "Bookings",
                column: "PaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Payment_PaymentId",
                table: "Bookings",
                column: "PaymentId",
                principalTable: "Payment",
                principalColumn: "PaymentId");
        }
    }
}
