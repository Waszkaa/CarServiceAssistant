using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarServiceAssistant.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleVin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Vin",
                table: "Vehicles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Vin",
                table: "Vehicles");
        }
    }
}
