using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BookingService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    VenueId = table.Column<int>(type: "int", nullable: false),
                    VenueName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VenueAddress = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PricePerHour = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Hours = table.Column<int>(type: "int", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Bookings",
                columns: new[] { "Id", "CreatedAt", "Hours", "IdempotencyKey", "PricePerHour", "StartTime", "Status", "TotalPrice", "UpdatedAt", "UserId", "VenueAddress", "VenueId", "VenueName" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 9, 10, 12, 0, 0, 0, DateTimeKind.Utc), 2, null, 500.00m, new DateTime(2026, 9, 12, 18, 0, 0, 0, DateTimeKind.Utc), "Confirmed", 1000.00m, new DateTime(2026, 9, 10, 12, 0, 0, 0, DateTimeKind.Utc), 1, "Main St 1", 1, "Central Football Field" },
                    { 3, new DateTime(2026, 10, 8, 10, 0, 0, 0, DateTimeKind.Utc), 2, null, 300.00m, new DateTime(2026, 10, 9, 7, 15, 44, 0, DateTimeKind.Utc), "Confirmed", 600.00m, new DateTime(2026, 10, 8, 10, 0, 0, 0, DateTimeKind.Utc), 1, "Park Ave 5", 2, "Tennis Club Kyiv" },
                    { 4, new DateTime(2026, 10, 15, 15, 0, 0, 0, DateTimeKind.Utc), 2, null, 500.00m, new DateTime(2026, 10, 20, 19, 0, 0, 0, DateTimeKind.Utc), "Confirmed", 1000.00m, new DateTime(2026, 10, 15, 15, 0, 0, 0, DateTimeKind.Utc), 1, "Main St 1", 1, "Central Football Field" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_IdempotencyKey",
                table: "Bookings",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status",
                table: "Bookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserId",
                table: "Bookings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_VenueId",
                table: "Bookings",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_VenueId_StartTime_Status",
                table: "Bookings",
                columns: new[] { "VenueId", "StartTime", "Status" });

            migrationBuilder.Sql("ALTER TABLE Bookings AUTO_INCREMENT = 100;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bookings");
        }
    }
}
