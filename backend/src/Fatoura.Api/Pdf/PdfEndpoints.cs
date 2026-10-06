using Fatoura.Api.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Fatoura.Api.Pdf;

public static class PdfEndpoints
{
    public static RouteGroupBuilder MapPdfEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/invoices/{id:int}/pdf", async (int id, bool? download, PdfService pdf, CancellationToken ct) =>
            File(await pdf.InvoiceAsync(id, ct), download)).WithTags("Invoices").RequireAuthorization(Policies.Staff);
        api.MapGet("/quotations/{id:int}/pdf", async (int id, bool? download, PdfService pdf, CancellationToken ct) =>
            File(await pdf.QuotationAsync(id, ct), download)).WithTags("Quotations").RequireAuthorization(Policies.Staff);
        api.MapGet("/credit-notes/{id:int}/pdf", async (int id, bool? download, PdfService pdf, CancellationToken ct) =>
            File(await pdf.CreditNoteAsync(id, ct), download)).WithTags("Credit Notes").RequireAuthorization(Policies.Staff);
        return api;
    }

    /// <summary>Inline by default (opens in the browser's viewer); <c>?download=true</c> forces a file download.</summary>
    private static FileContentHttpResult File((byte[] Pdf, string FileName) doc, bool? download) =>
        download == true
            ? TypedResults.File(doc.Pdf, "application/pdf", doc.FileName)
            : TypedResults.File(doc.Pdf, "application/pdf");
}
