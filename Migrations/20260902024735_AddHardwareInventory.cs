using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalCarrierApp.API.Migrations
{
    /// <inheritdoc />
    public partial class AddHardwareInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlantId = table.Column<int>(type: "int", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Processor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RamGb = table.Column<int>(type: "int", nullable: true),
                    StorageType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StorageSizeGb = table.Column<int>(type: "int", nullable: true),
                    Gpu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OperatingSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WarrantyStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WarrantyEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventory", x => x.Id);
                    table.CheckConstraint("CK_Inventory_DeviceType", "[DeviceType] IS NULL OR [DeviceType] IN ('Laptop','Desktop','Workstation','Server')");
                    table.CheckConstraint("CK_Inventory_RamGb", "[RamGb] IS NULL OR [RamGb] >= 0");
                    table.CheckConstraint("CK_Inventory_Status", "[Status] IN ('Active','In Repair','Retired','Spare')");
                    table.CheckConstraint("CK_Inventory_StorageSizeGb", "[StorageSizeGb] IS NULL OR [StorageSizeGb] >= 0");
                    table.CheckConstraint("CK_Inventory_StorageType", "[StorageType] IS NULL OR [StorageType] IN ('HDD','SSD','NVMe','Hybrid')");
                    table.CheckConstraint("CK_Inventory_WarrantyDates", "[WarrantyStartDate] IS NULL OR [WarrantyEndDate] IS NULL OR [WarrantyEndDate] >= [WarrantyStartDate]");
                    table.ForeignKey(
                        name: "FK_Inventory_Plants_PlantId",
                        column: x => x.PlantId,
                        principalTable: "Plants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_PlantId",
                table: "Inventory",
                column: "PlantId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_SerialNumber",
                table: "Inventory",
                column: "SerialNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inventory");
        }
    }
}
