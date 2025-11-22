using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.Migrations
{
    /// <inheritdoc />
    public partial class dbbb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemporary",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Bank",
                table: "Payment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CVV",
                table: "Payment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardNumber",
                table: "Payment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpiryDate",
                table: "Payment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePaymentIntentId",
                table: "Payment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSessionId",
                table: "Payment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalletId",
                table: "Payment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalletProvider",
                table: "Payment",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTemporary",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Bank",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "CVV",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "CardNumber",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "StripePaymentIntentId",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "StripeSessionId",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "WalletProvider",
                table: "Payment");
        }
    }
}
