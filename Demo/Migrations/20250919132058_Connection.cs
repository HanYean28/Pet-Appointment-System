using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.Migrations
{
    /// <inheritdoc />
    public partial class Connection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "LoginHistories",
                type: "nvarchar(100)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistories_Email",
                table: "LoginHistories",
                column: "Email");

            migrationBuilder.AddForeignKey(
                name: "FK_LoginHistories_Users_Email",
                table: "LoginHistories",
                column: "Email",
                principalTable: "Users",
                principalColumn: "Email",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoginHistories_Users_Email",
                table: "LoginHistories");

            migrationBuilder.DropIndex(
                name: "IX_LoginHistories_Email",
                table: "LoginHistories");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "LoginHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)");
        }
    }
}
