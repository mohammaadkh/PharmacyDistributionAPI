using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PharmacyAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialProMax12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_Medicines_MedicineId",
                table: "CartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Medicines_MedicineId",
                table: "OrderDetails");

            migrationBuilder.AlterColumn<string>(
                name: "PackSize",
                table: "Medicines",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Bacterial infections", "Antibiotics" },
                    { 2, "Pain relief", "Painkillers" }
                });

            migrationBuilder.InsertData(
                table: "Medicines",
                columns: new[] { "Id", "BlackBoxWarning", "CategoryId", "ClinicalSpecs", "ControlledSubstance", "Description", "Dosage", "FdaApprovalDate", "HumidityLimit", "ImageUrl", "IsColdChain", "IsFdaApproved", "IsGmpCertified", "Manufacturer", "Name", "NdcNumber", "PackSize", "Price", "SKU", "StockQuantity", "TemperatureRange" },
                values: new object[,]
                {
                    { 1, null, 2, null, "Non-Controlled", "", "500mg", null, null, "/images/default.png", false, true, false, "GSK", "Panadol", null, "", 12.50m, "PAN-001", 100, null },
                    { 2, null, 1, null, "Non-Controlled", "", "250mg", null, null, "/images/default.png", false, false, false, "Pfizer", "Amoxicillin", null, "", 45.00m, "AMO-002", 50, null }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_Medicines_MedicineId",
                table: "CartItems",
                column: "MedicineId",
                principalTable: "Medicines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Medicines_MedicineId",
                table: "OrderDetails",
                column: "MedicineId",
                principalTable: "Medicines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_Medicines_MedicineId",
                table: "CartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Medicines_MedicineId",
                table: "OrderDetails");

            migrationBuilder.DeleteData(
                table: "Medicines",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Medicines",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.AlterColumn<string>(
                name: "PackSize",
                table: "Medicines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_Medicines_MedicineId",
                table: "CartItems",
                column: "MedicineId",
                principalTable: "Medicines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Medicines_MedicineId",
                table: "OrderDetails",
                column: "MedicineId",
                principalTable: "Medicines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
