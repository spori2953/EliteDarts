using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDarts.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchStateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentDartInVisit",
                table: "Matches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentLegNo",
                table: "Matches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentPlayerId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegStarterPlayerId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "P1Remaining",
                table: "Matches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "P2Remaining",
                table: "Matches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VisitStartRemaining",
                table: "Matches",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentDartInVisit",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "CurrentLegNo",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "CurrentPlayerId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "LegStarterPlayerId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "P1Remaining",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "P2Remaining",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "VisitStartRemaining",
                table: "Matches");
        }
    }
}
