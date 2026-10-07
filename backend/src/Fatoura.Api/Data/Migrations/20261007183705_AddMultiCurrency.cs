using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fatoura.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Quotations",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "AED");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Quotations",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotalAed",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAed",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatTotalAed",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAed",
                table: "QuotationLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAed",
                table: "QuotationLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAed",
                table: "PurchaseLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAed",
                table: "PurchaseLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PurchaseInvoices",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "AED");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "PurchaseInvoices",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotalAed",
                table: "PurchaseInvoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAed",
                table: "PurchaseInvoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatTotalAed",
                table: "PurchaseInvoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Invoices",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "AED");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Invoices",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotalAed",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAed",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatTotalAed",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAed",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAed",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "CreditNotes",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "AED");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "CreditNotes",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotalAed",
                table: "CreditNotes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAed",
                table: "CreditNotes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatTotalAed",
                table: "CreditNotes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAed",
                table: "CreditNoteLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAed",
                table: "CreditNoteLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CurrencyRates",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RateToAed = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyRates", x => x.Code);
                });

            // Existing documents are all in AED, so their AED amounts equal their own.
            foreach (var table in new[] { "Quotations", "Invoices", "CreditNotes", "PurchaseInvoices" })
            {
                migrationBuilder.Sql($"UPDATE [{table}] SET [SubTotalAed] = [SubTotal], [VatTotalAed] = [VatTotal], [TotalAed] = [Total];");
            }

            foreach (var table in new[] { "QuotationLines", "InvoiceLines", "CreditNoteLines", "PurchaseLines" })
            {
                migrationBuilder.Sql($"UPDATE [{table}] SET [NetAed] = [Net], [VatAed] = [Vat];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurrencyRates");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "SubTotalAed",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "TotalAed",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "VatTotalAed",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "NetAed",
                table: "QuotationLines");

            migrationBuilder.DropColumn(
                name: "VatAed",
                table: "QuotationLines");

            migrationBuilder.DropColumn(
                name: "NetAed",
                table: "PurchaseLines");

            migrationBuilder.DropColumn(
                name: "VatAed",
                table: "PurchaseLines");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "SubTotalAed",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "TotalAed",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "VatTotalAed",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SubTotalAed",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TotalAed",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VatTotalAed",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "NetAed",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "VatAed",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "SubTotalAed",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "TotalAed",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "VatTotalAed",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "NetAed",
                table: "CreditNoteLines");

            migrationBuilder.DropColumn(
                name: "VatAed",
                table: "CreditNoteLines");
        }
    }
}
