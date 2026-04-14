using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDarts.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingCheckoutDartNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PendingCheckoutDartNo",
                table: "Matches",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingCheckoutDartNo",
                table: "Matches");
        }
    }
}
