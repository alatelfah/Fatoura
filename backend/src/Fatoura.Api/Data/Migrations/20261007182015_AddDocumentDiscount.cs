using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fatoura.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountKind",
                table: "Quotations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None"); // existing documents have no discount

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "QuotationLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "PurchaseLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountKind",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None"); // existing documents have no discount

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "CreditNotes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "CreditNoteLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discount",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "DiscountKind",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "QuotationLines");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "PurchaseLines");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountKind",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "CreditNoteLines");
        }
    }
}
