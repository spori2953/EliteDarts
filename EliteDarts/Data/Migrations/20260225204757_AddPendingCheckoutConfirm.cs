using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDarts.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingCheckoutConfirm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PendingCheckoutConfirm",
                table: "Matches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PendingCheckoutRing",
                table: "Matches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingCheckoutSector",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingCheckoutWinnerPlayerId",
                table: "Matches",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingCheckoutConfirm",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "PendingCheckoutRing",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "PendingCheckoutSector",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "PendingCheckoutWinnerPlayerId",
                table: "Matches");
        }
    }
}
