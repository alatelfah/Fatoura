using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fatoura.Api.Pdf;

/// <summary>
/// Tax invoice / quotation / credit note layout, matched to the reference invoice
/// (docs/reference/Aura Suites Downtown 260819.pdf): US Letter landscape, Calibri metrics (Carlito),
/// company block and logo, boxed title, client and number blocks, bordered line table with a light-blue header,
/// stamp beside the totals and the terms footer.
/// </summary>
public sealed class SalesDocumentPdf(PrintDocument doc) : IDocument
{
    public const string BodyFont = "Carlito";
    public const string ArabicFont = "Noto Naskh Arabic";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly Color HeaderFill = Color.FromHex("#DDEBF7");
    private const float Outer = 1.5f;
    private const float Inner = 0.75f;

    public static string Money(decimal value) => value.ToString("#,##0.00", Invariant);

    public static string Quantity(decimal value) => value.ToString("0.###", Invariant);

    public static string Date(DateOnly value) => value.ToString("d-MMM-yy", Invariant);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{doc.Title} {doc.Number}",
        Author = doc.Company.Name,
        Creator = "Fatoura",
        Producer = "Fatoura",
    };

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter.Landscape());
            // Margins measured from the reference invoice.
            page.MarginLeft(94);
            page.MarginRight(62);
            page.MarginTop(52);
            page.MarginBottom(24);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t.FontFamily(BodyFont, ArabicFont).FontSize(10).FontColor(Colors.Black));

            if (doc.IsVoid)
            {
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-30)
                    .Text("VOID").FontSize(140).Bold().FontColor(Color.FromHex("#33CC0000"));
            }

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Darken1));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem(1.25f).PaddingTop(10).Column(c =>
                {
                    c.Spacing(1);
                    c.Item().Text(doc.Company.Name).Bold();
                    if (!string.IsNullOrWhiteSpace(doc.Company.Address))
                    {
                        c.Item().Text(doc.Company.Address).Bold();
                    }

                    if (!string.IsNullOrWhiteSpace(doc.Company.Phone))
                    {
                        c.Item().Text($"Contact No: {doc.Company.Phone}").Bold();
                    }

                    c.Item().Text($"TRN : {doc.Company.Trn}").Bold();
                    var contact = string.Join(" | ", new[]
                    {
                        string.IsNullOrWhiteSpace(doc.Company.Email) ? null : $"Email: {doc.Company.Email}",
                        string.IsNullOrWhiteSpace(doc.Company.Website) ? null : $"Website: {doc.Company.Website}",
                    }.Where(x => x is not null));
                    if (contact.Length > 0)
                    {
                        c.Item().Text(contact).Bold();
                    }
                });

                row.RelativeItem(1).Height(84).AlignLeft().AlignMiddle().Element(e =>
                {
                    if (doc.Company.Logo is { Length: > 0 } logo)
                    {
                        e.Image(logo).FitArea();
                    }
                });
            });

            col.Item().PaddingTop(16).Border(Outer).PaddingVertical(2).AlignCenter()
                .Text(doc.Title).FontSize(15).Bold();
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingTop(12).Column(col =>
        {
            col.Item().Element(ComposeParties);
            col.Item().PaddingTop(12).Element(ComposeLines);
            col.Item().PaddingTop(6).ShowEntire().Element(ComposeTotals);
            col.Item().PaddingTop(2).EnsureSpace(60).Element(ComposeTerms);
        });
    }

    private void ComposeParties(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem(3).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(62);
                    c.RelativeColumn();
                });
                void Field(string label, string value, bool boldValue)
                {
                    t.Cell().PaddingBottom(1).Text(label).Bold();
                    var text = t.Cell().PaddingBottom(1).Text(value);
                    if (boldValue)
                    {
                        text.Bold();
                    }
                }

                Field("Client Name:", doc.Client.Name, boldValue: true);
                Field("Address:", doc.Client.Address, boldValue: false);
                Field("Contact No:", doc.Client.Phone, boldValue: false);
                Field("TRN", doc.Client.Trn, boldValue: true);
            });

            row.ConstantItem(200).AlignBottom().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1);
                    c.RelativeColumn(1.25f);
                });
                t.Cell().PaddingBottom(1).AlignRight().PaddingRight(8).Text(doc.NumberLabel).Bold();
                t.Cell().PaddingBottom(1).AlignLeft().Text(doc.Number);
                foreach (var info in doc.ExtraInfo)
                {
                    t.Cell().PaddingBottom(1).AlignRight().PaddingRight(8).Text(info.Label).Bold();
                    t.Cell().PaddingBottom(1).AlignLeft().Text(info.Value);
                }

                t.Cell().PaddingBottom(1).AlignRight().PaddingRight(8).Text("Date:").Bold();
                t.Cell().PaddingBottom(1).AlignRight().Text(Date(doc.Date)).Bold();
            });
        });
    }

    private bool HasDiscount => doc.Discount != 0;

    private void ComposeLines(IContainer container)
    {
        // With a discount, a Discount column takes its width from the description so each row still adds up.
        var columns = new List<(string Title, float Width)>
        {
            ("Sl.No", 9.7f),
            ("Description of Service", HasDiscount ? 37.6f : 47.6f),
            ("Qty", 4.2f),
            ("Unit Price (AED)", 12.5f),
        };
        if (HasDiscount)
        {
            columns.Add(("Discount", 10f));
        }

        columns.Add(("Vat", 12.0f));
        columns.Add(("Amount (AED)", 14.0f));

        container.Border(Outer).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                foreach (var (_, width) in columns)
                {
                    c.RelativeColumn(width);
                }
            });

            t.Header(h =>
            {
                foreach (var (title, _) in columns)
                {
                    h.Cell().Background(HeaderFill).BorderBottom(Outer).BorderRight(Inner).PaddingVertical(2).PaddingHorizontal(3)
                        .AlignCenter().Text(title).Bold();
                }
            });

            foreach (var line in doc.Lines)
            {
                Cell(t).AlignCenter().Text(line.No.ToString(Invariant));
                Cell(t).AlignCenter().Text(line.Description).Bold();
                Cell(t).AlignCenter().Text(Quantity(line.Quantity));
                Cell(t).AlignRight().Text(Money(line.UnitPrice));
                if (HasDiscount)
                {
                    Cell(t).AlignRight().Text(Money(line.Discount));
                }

                Cell(t).AlignRight().Text(Money(line.Vat));
                Cell(t).AlignRight().Text(Money(line.Amount));
            }

            // Trailing empty row, as on the reference invoice.
            for (var i = 0; i < columns.Count; i++)
            {
                t.Cell().BorderRight(Inner).MinHeight(22);
            }
        });

        static IContainer Cell(TableDescriptor t) =>
            t.Cell().BorderBottom(Inner).BorderRight(Inner).MinHeight(24).PaddingVertical(4).PaddingHorizontal(4).AlignMiddle();
    }

    private void ComposeTotals(IContainer container)
    {
        var rows = new List<(string Label, decimal Value)> { ("Sub Total", doc.SubTotal + doc.Discount) };
        if (HasDiscount)
        {
            rows.Add(("Discount", -doc.Discount));
            rows.Add(("Total excl. VAT", doc.SubTotal));
        }

        rows.Add((doc.VatLabel, doc.VatTotal));

        container.Row(row =>
        {
            // Column proportions measured from the reference: stamp centred under the description, totals at the right.
            row.RelativeItem(38.5f);
            row.RelativeItem(14f).Height(72).AlignCenter().AlignMiddle().Element(e =>
            {
                if (doc.Company.Stamp is { Length: > 0 } stamp)
                {
                    e.Image(stamp).FitArea();
                }
                else
                {
                    e.Height(64).Border(Inner).BorderColor(Colors.Grey.Medium).AlignCenter().AlignMiddle()
                        .Text("Stamp & Signature").FontSize(8).FontColor(Colors.Grey.Darken1);
                }
            });
            row.RelativeItem(20.2f).PaddingTop(8).Column(c =>
            {
                foreach (var (label, _) in rows)
                {
                    c.Item().AlignRight().Text(label).Bold();
                }

                c.Item().AlignRight().Text("Total Amount").Bold();
            });
            row.RelativeItem(26.6f).PaddingTop(8).PaddingRight(4).Column(c =>
            {
                foreach (var (_, value) in rows)
                {
                    c.Item().AlignRight().Text(Money(value));
                }

                c.Item().AlignRight().Text(Money(doc.Total)).Bold();
            });
        });
    }

    private void ComposeTerms(IContainer container)
    {
        container.DefaultTextStyle(s => s.FontSize(8)).Column(c =>
        {
            var t = doc.Terms;
            if (!string.IsNullOrWhiteSpace(t.PaymentTerms) || !string.IsNullOrWhiteSpace(t.CompletionOfWork))
            {
                c.Item().Text("Terms and Conditions :-");
            }

            if (!string.IsNullOrWhiteSpace(t.PaymentTerms))
            {
                c.Item().Text($"Payment Terms :- {t.PaymentTerms}");
            }

            if (!string.IsNullOrWhiteSpace(t.CompletionOfWork))
            {
                c.Item().Text($"Completion of Work: {t.CompletionOfWork}");
            }

            if (t.Notes.Count > 0)
            {
                c.Item().Text("Note :");
                for (var i = 0; i < t.Notes.Count; i++)
                {
                    c.Item().Text($"{i + 1}. {t.Notes[i]}");
                }
            }

            if (!string.IsNullOrWhiteSpace(t.ClosingText))
            {
                c.Item().Text(t.ClosingText);
            }

            c.Item().Text($"For {doc.Company.Name}");
        });
    }
}
