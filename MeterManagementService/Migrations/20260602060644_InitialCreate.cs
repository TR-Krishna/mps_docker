using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeterManagementService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MeterType = table.Column<int>(type: "integer", nullable: false),
                    IsSmart = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrepaid = table.Column<bool>(type: "boolean", nullable: false),
                    AccuracyClass = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VoltageRating = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CurrentRating = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Standards = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CommunicationType = table.Column<int>(type: "integer", nullable: false),
                    DcuId = table.Column<string>(type: "text", nullable: true),
                    SimNumber = table.Column<string>(type: "text", nullable: true),
                    DlmsLogicalDeviceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsumerNumber = table.Column<string>(type: "text", nullable: true),
                    InstallationAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubstationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RelayStatus = table.Column<int>(type: "integer", nullable: false),
                    FirmwareVersion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TodEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OnboardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastCommunicatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastBillingReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastConfiguredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Meters_SerialNumber",
                table: "Meters",
                column: "SerialNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Meters");
        }
    }
}
