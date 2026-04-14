using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDarts.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayNameToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Calibration",
                table: "Boards");

            migrationBuilder.DropColumn(
                name: "CameraAId",
                table: "Boards");

            migrationBuilder.DropColumn(
                name: "CameraBId",
                table: "Boards");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "Calibration",
                table: "Boards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraAId",
                table: "Boards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraBId",
                table: "Boards",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
