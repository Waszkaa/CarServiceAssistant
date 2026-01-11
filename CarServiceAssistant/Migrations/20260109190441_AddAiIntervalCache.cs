using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarServiceAssistant.Migrations
{
    /// <inheritdoc />
    public partial class AddAiIntervalCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiIntervalCaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Area = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiIntervalCaches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiIntervalCaches_VehicleId_Area",
                table: "AiIntervalCaches",
                columns: new[] { "VehicleId", "Area" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiIntervalCaches");
        }
    }
}
