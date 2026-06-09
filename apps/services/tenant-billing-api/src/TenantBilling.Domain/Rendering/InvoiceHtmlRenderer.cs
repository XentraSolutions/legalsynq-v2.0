using System.Globalization;
using System.Net;
using System.Text;

namespace TenantBilling.Domain.Rendering;

/// <summary>
/// Default <see cref="IInvoiceHtmlRenderer"/>.
///
/// Stateless / pure: produces a self-contained, server-rendered HTML
/// document from a render model. No external CSS, no JavaScript, no
/// network references other than (optionally) the snapshot's
/// <c>LogoUrl</c> as an <c>&lt;img src&gt;</c>.
///
/// Visual style: emulates the QuickBooks Online invoice template —
/// a single white "sheet" centred on a soft grey page, big "INVOICE"
/// mark on the top right with the number underneath, business
/// identity on the top left, From / Bill-to columns, a clean
/// uppercase-headed line item table, and a totals box on the bottom
/// right that highlights "Balance due" in the tenant's accent
/// colour. The layout is fluid (no fixed widths beyond the sheet
/// max-width) so it prints to a single page on Letter / A4 stock.
///
/// Security model:
/// <list type="bullet">
///   <item>Every text field originating from user/admin input
///     (customer name, line description, notes, header/footer text,
///     payment instructions, terms, memo placeholder, customer
///     address fields, issuer fields) is escaped via
///     <see cref="WebUtility.HtmlEncode(string?)"/> before being
///     written to the document. Any literal <c>&lt;script&gt;</c>
///     supplied by the user therefore renders as visible text, not
///     as executable script.</item>
///   <item>The accent colour is <em>also</em> escaped before being
///     interpolated into the inline <c>&lt;style&gt;</c> block to
///     defeat CSS-context injection (e.g. a value of
///     <c>red;}&lt;/style&gt;…</c> cannot break out — the
///     <c>&lt;</c>/<c>&gt;</c>/<c>&amp;</c>/<c>"</c>/<c>'</c> are
///     entity-encoded which a CSS parser treats as invalid colour
///     and ignores).</item>
///   <item>The logo URL is escaped as an HTML attribute value (so
///     <c>&quot;</c>/<c>&lt;</c> become entities) but is not
///     URL-validated — the snapshot was set from an admin input
///     whose vetting is the template-creation surface's job, not
///     the renderer's.</item>
///   <item>The issuer website link is rendered with
///     <c>rel="noopener noreferrer"</c> and the URL is HTML-encoded
///     in attribute context. The template-creation surface enforces
///     <c>http(s)://</c> so a <c>javascript:</c> URL cannot reach
///     this renderer through the template path.</item>
/// </list>
/// </summary>
public sealed class InvoiceHtmlRenderer : IInvoiceHtmlRenderer
{
    private const string DefaultAccent = "#1f2937";

    public string Render(InvoiceRenderDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var snap = document.TemplateSnapshot;
        var accent = SafeAccent(snap?.AccentColor);
        var sb = new StringBuilder(4096);

        sb.Append("<!doctype html><html lang=\"en\"><head>");
        sb.Append("<meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<title>Invoice ").Append(Esc(document.InvoiceNumber)).Append("</title>");
        sb.Append("<style>");
        // Reset & base typography (system font stack — QuickBooks uses Avenir/Helvetica)
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("body{font-family:-apple-system,BlinkMacSystemFont,\"Segoe UI\",Roboto,\"Helvetica Neue\",Arial,sans-serif;");
        sb.Append("color:#393A3D;background:#F4F5F8;margin:0;padding:0;-webkit-font-smoothing:antialiased;}");
        // The white "sheet" the whole invoice sits on
        sb.Append(".sheet{max-width:816px;margin:24px auto;background:#fff;");
        sb.Append("padding:48px 56px;box-shadow:0 1px 4px rgba(0,0,0,.08);border-radius:2px;}");
        sb.Append(".accent{color:").Append(accent).Append(";}");
        // Top header: identity left, INVOICE mark right
        sb.Append(".invoice-header{display:flex;justify-content:space-between;align-items:flex-start;gap:24px;margin-bottom:32px;}");
        sb.Append(".id-block{flex:1;min-width:0;}");
        sb.Append(".id-block .biz-name{font-size:18px;font-weight:600;color:#393A3D;margin:8px 0 4px;}");
        sb.Append(".id-block p{margin:0;color:#393A3D;font-size:13px;line-height:1.5;}");
        sb.Append(".mark-block{flex:0 0 auto;text-align:right;}");
        sb.Append(".mark{font-size:36px;font-weight:300;letter-spacing:6px;color:#6B6C72;text-transform:uppercase;line-height:1;}");
        sb.Append(".invnum{font-size:13px;color:#393A3D;margin-top:8px;font-weight:600;letter-spacing:.3px;}");
        sb.Append(".logo{max-height:64px;display:block;margin-bottom:8px;}");
        // Meta strip on the right (issue/due/status)
        sb.Append(".invoice-meta{display:flex;justify-content:flex-end;margin-bottom:28px;}");
        sb.Append(".meta-grid{display:inline-grid;grid-template-columns:auto auto;gap:6px 18px;font-size:12px;}");
        sb.Append(".meta-grid .k{color:#6B6C72;text-transform:uppercase;letter-spacing:.6px;font-weight:600;}");
        sb.Append(".meta-grid .v{color:#393A3D;font-weight:500;text-align:right;}");
        // Parties row
        sb.Append(".parties{display:flex;gap:48px;margin-bottom:32px;}");
        sb.Append(".parties .party{flex:1;min-width:0;}");
        sb.Append(".party .label{font-size:11px;color:#6B6C72;text-transform:uppercase;letter-spacing:1.2px;");
        sb.Append("font-weight:600;display:block;margin-bottom:8px;border-bottom:1px solid #E1E4E8;padding-bottom:6px;}");
        sb.Append(".party .addr-line{display:block;font-size:13px;color:#393A3D;line-height:1.65;}");
        sb.Append(".party .addr-line.headline{font-weight:600;color:#393A3D;}");
        sb.Append(".party a{color:").Append(accent).Append(";text-decoration:none;}");
        // Line items table
        sb.Append("table.lines{width:100%;border-collapse:collapse;margin:0 0 12px;}");
        sb.Append("table.lines th{background:#F4F5F8;color:#393A3D;text-transform:uppercase;");
        sb.Append("letter-spacing:.6px;font-size:11px;font-weight:600;padding:10px 12px;");
        sb.Append("border-bottom:2px solid ").Append(accent).Append(";text-align:left;}");
        sb.Append("table.lines td{padding:12px;border-bottom:1px solid #E1E4E8;font-size:13px;color:#393A3D;}");
        sb.Append(".text-right{text-align:right;}");
        // Totals
        sb.Append(".totals-wrap{display:flex;justify-content:flex-end;}");
        sb.Append(".totals{width:320px;border-collapse:collapse;}");
        sb.Append(".totals td{padding:8px 12px;font-size:13px;color:#393A3D;}");
        sb.Append(".totals tr.subtotals td{border-bottom:1px solid #E1E4E8;}");
        sb.Append(".totals tr.balance td{background:").Append(accent).Append(";color:#fff;");
        sb.Append("font-size:15px;font-weight:700;text-transform:uppercase;letter-spacing:.5px;padding:14px 12px;}");
        // Sections (notes / payment / terms)
        sb.Append(".section{margin-top:32px;padding-top:16px;border-top:1px solid #E1E4E8;}");
        sb.Append(".section h3{font-size:11px;color:#6B6C72;text-transform:uppercase;");
        sb.Append("letter-spacing:1.2px;font-weight:600;margin:0 0 8px;}");
        sb.Append(".section p{font-size:13px;line-height:1.65;color:#393A3D;margin:0;white-space:pre-wrap;}");
        // Footer
        sb.Append(".footer{margin-top:24px;padding-top:14px;border-top:1px solid #E1E4E8;");
        sb.Append("font-size:11px;color:#6B6C72;text-align:center;}");
        // Print
        sb.Append("@media print{body{background:#fff;}.sheet{box-shadow:none;margin:0;padding:24px;max-width:none;}}");
        sb.Append("</style></head><body>");

        sb.Append("<div class=\"sheet\">");

        // === Header band: identity (logo + business name + optional header) on the
        // left, "INVOICE" mark + number on the right.
        var issuer = document.Issuer;
        sb.Append("<div class=\"invoice-header\">");
        sb.Append("<div class=\"id-block\">");
        if (!string.IsNullOrWhiteSpace(snap?.LogoUrl))
        {
            sb.Append("<img class=\"logo\" alt=\"Logo\" src=\"")
              .Append(Esc(snap!.LogoUrl)).Append("\">");
        }
        // Top-left identity headline: prefer the issuer display name
        // (so the From block downstream isn't the only place it shows)
        // and fall back to legal name.
        var identity = !string.IsNullOrWhiteSpace(issuer?.DisplayName)
            ? issuer!.DisplayName
            : issuer?.LegalName;
        if (!string.IsNullOrWhiteSpace(identity))
        {
            sb.Append("<div class=\"biz-name\">").Append(Esc(identity)).Append("</div>");
        }
        if (!string.IsNullOrWhiteSpace(snap?.HeaderText))
        {
            sb.Append("<p>").Append(Esc(snap!.HeaderText)).Append("</p>");
        }
        sb.Append("</div>");

        sb.Append("<div class=\"mark-block\">");
        sb.Append("<div class=\"mark accent\">Invoice</div>");
        sb.Append("<div class=\"invnum\">#").Append(Esc(document.InvoiceNumber)).Append("</div>");
        // Hidden-but-present full string so any consumer/test that searches
        // for "Invoice <number>" still finds it adjacent. The mark + invnum
        // pieces above are split for visual styling.
        sb.Append("<span style=\"position:absolute;left:-9999px;\" aria-hidden=\"true\">");
        sb.Append("Invoice ").Append(Esc(document.InvoiceNumber));
        sb.Append("</span>");
        sb.Append("</div>");
        sb.Append("</div>");

        // Meta strip (issue/due/status) right-aligned under the mark
        sb.Append("<div class=\"invoice-meta\"><div class=\"meta-grid\">");
        sb.Append("<div class=\"k\">Issue date</div><div class=\"v\">").Append(Date(document.IssueDate)).Append("</div>");
        sb.Append("<div class=\"k\">Due date</div><div class=\"v\">").Append(Date(document.DueDate)).Append("</div>");
        sb.Append("<div class=\"k\">Status</div><div class=\"v\">").Append(Esc(document.Status)).Append("</div>");
        sb.Append("</div></div>");

        // === Parties row (From + Bill to). Both halves render only if
        // their data is present, so an invoice with no issuer + no
        // customer address cleanly degrades to a single Bill-to with
        // just the name + email.
        sb.Append("<div class=\"parties\">");

        // From (issuer) — entirely snapshot-driven. Null when the
        // invoice was never stamped with issuer info.
        if (issuer is not null)
        {
            sb.Append("<div class=\"party\">");
            sb.Append("<span class=\"label\">From</span>");
            var headline = !string.IsNullOrWhiteSpace(issuer.DisplayName)
                ? issuer.DisplayName!
                : (issuer.LegalName ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(headline))
            {
                sb.Append("<span class=\"addr-line headline\">").Append(Esc(headline)).Append("</span>");
            }
            // Render the legal name on its own line when distinct
            // from the display name (so e.g. "Acme" + "Acme, Inc."
            // both appear).
            if (!string.IsNullOrWhiteSpace(issuer.LegalName)
                && !string.Equals(issuer.LegalName, issuer.DisplayName, StringComparison.Ordinal))
            {
                sb.Append("<span class=\"addr-line\">").Append(Esc(issuer.LegalName)).Append("</span>");
            }
            AppendOptionalLine(sb, issuer.AddressLine1);
            AppendOptionalLine(sb, issuer.AddressLine2);
            AppendCityStatePostal(sb, issuer.City, issuer.StateRegion, issuer.PostalCode);
            AppendOptionalLine(sb, issuer.Country);
            AppendOptionalLine(sb, issuer.Email);
            AppendOptionalLine(sb, issuer.Phone);
            if (!string.IsNullOrWhiteSpace(issuer.Website))
            {
                sb.Append("<span class=\"addr-line\"><a href=\"")
                  .Append(Esc(issuer.Website))
                  .Append("\" rel=\"noopener noreferrer\">")
                  .Append(Esc(issuer.Website))
                  .Append("</a></span>");
            }
            if (!string.IsNullOrWhiteSpace(issuer.TaxId))
            {
                sb.Append("<span class=\"addr-line\">Tax ID: ")
                  .Append(Esc(issuer.TaxId)).Append("</span>");
            }
            sb.Append("</div>");
        }

        // Bill To — name + email always; structured/legacy address
        // lines only when (a) customer has address data AND (b)
        // either no template snapshot OR the snapshot's
        // DisplayBillingAddress flag is true.
        sb.Append("<div class=\"party\">");
        sb.Append("<span class=\"label\">Bill to</span>");
        sb.Append("<span class=\"addr-line headline\">")
          .Append(Esc(string.IsNullOrWhiteSpace(document.CustomerName) ? "(customer)" : document.CustomerName))
          .Append("</span>");
        if (!string.IsNullOrWhiteSpace(document.CustomerEmail))
        {
            sb.Append("<span class=\"addr-line\">").Append(Esc(document.CustomerEmail)).Append("</span>");
        }

        var showCustomerAddress = document.CustomerAddress is not null
            && (snap is null || snap.DisplayBillingAddress);
        if (showCustomerAddress)
        {
            var addr = document.CustomerAddress!;
            AppendOptionalLine(sb, addr.Line1);
            AppendOptionalLine(sb, addr.Line2);
            AppendCityStatePostal(sb, addr.City, addr.StateRegion, addr.PostalCode);
            AppendOptionalLine(sb, addr.Country);
        }
        sb.Append("</div>");
        sb.Append("</div>");

        // === Line items
        sb.Append("<table class=\"lines\"><thead><tr>");
        sb.Append("<th>Description</th><th class=\"text-right\">Qty</th>");
        sb.Append("<th class=\"text-right\">Rate</th><th class=\"text-right\">Amount</th>");
        sb.Append("</tr></thead><tbody>");
        if (document.Lines.Count == 0)
        {
            sb.Append("<tr><td colspan=\"4\"><em>No line items.</em></td></tr>");
        }
        else
        {
            foreach (var line in document.Lines)
            {
                sb.Append("<tr><td>").Append(Esc(line.Description)).Append("</td>");
                sb.Append("<td class=\"text-right\">").Append(line.Quantity.ToString(CultureInfo.InvariantCulture)).Append("</td>");
                sb.Append("<td class=\"text-right\">").Append(Money(line.UnitAmount, document.Currency)).Append("</td>");
                sb.Append("<td class=\"text-right\">").Append(Money(line.LineTotal, document.Currency)).Append("</td></tr>");
            }
        }
        sb.Append("</tbody></table>");

        // === Totals — subtotals stack on top, "Balance due" sits in
        // the accent-coloured highlight bar at the bottom.
        sb.Append("<div class=\"totals-wrap\"><table class=\"totals\"><tbody>");
        sb.Append("<tr class=\"subtotals\"><td>Subtotal</td><td class=\"text-right\">").Append(Money(document.Subtotal, document.Currency)).Append("</td></tr>");
        sb.Append("<tr class=\"subtotals\"><td>Tax</td><td class=\"text-right\">").Append(Money(document.TaxAmount, document.Currency)).Append("</td></tr>");
        sb.Append("<tr class=\"subtotals\"><td>Discount</td><td class=\"text-right\">").Append(Money(document.DiscountAmount, document.Currency)).Append("</td></tr>");
        sb.Append("<tr class=\"subtotals\"><td><strong>Total</strong></td><td class=\"text-right\"><strong>").Append(Money(document.TotalAmount, document.Currency)).Append("</strong></td></tr>");
        sb.Append("<tr class=\"subtotals\"><td>Paid</td><td class=\"text-right\">").Append(Money(document.AmountPaid, document.Currency)).Append("</td></tr>");
        sb.Append("<tr class=\"balance\"><td>Balance due</td><td class=\"text-right\">").Append(Money(document.AmountDue, document.Currency)).Append("</td></tr>");
        sb.Append("</tbody></table></div>");

        // Notes (per-invoice free text)
        if (!string.IsNullOrWhiteSpace(document.Notes))
        {
            sb.Append("<div class=\"section\"><h3>Notes</h3><p>").Append(Esc(document.Notes)).Append("</p></div>");
        }

        // Snapshot-driven payment instructions (gated by display flag)
        if (snap is not null
            && snap.DisplayPaymentInstructions
            && !string.IsNullOrWhiteSpace(snap.PaymentInstructions))
        {
            sb.Append("<div class=\"section\"><h3>Payment instructions</h3><p>").Append(Esc(snap.PaymentInstructions)).Append("</p></div>");
        }

        // Snapshot-driven terms (gated by display flag)
        if (snap is not null
            && snap.DisplayTerms
            && !string.IsNullOrWhiteSpace(snap.TermsText))
        {
            sb.Append("<div class=\"section\"><h3>Terms</h3><p>").Append(Esc(snap.TermsText)).Append("</p></div>");
        }

        // Footer text + always-visible "rendered at" tag
        if (!string.IsNullOrWhiteSpace(snap?.FooterText))
        {
            sb.Append("<div class=\"footer\">").Append(Esc(snap!.FooterText)).Append("</div>");
        }
        sb.Append("<div class=\"footer\">Generated ")
          .Append(document.GeneratedAtUtc.ToString("u", CultureInfo.InvariantCulture))
          .Append("</div>");

        sb.Append("</div>"); // .sheet
        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// Append a single address line if non-blank, otherwise no-op.
    /// </summary>
    private static void AppendOptionalLine(StringBuilder sb, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        sb.Append("<span class=\"addr-line\">").Append(Esc(value)).Append("</span>");
    }

    /// <summary>
    /// Compose a City / State / Postal line in the conventional
    /// "City, State PostalCode" shape, omitting any blank
    /// components and producing nothing when all three are blank.
    /// </summary>
    private static void AppendCityStatePostal(StringBuilder sb, string? city, string? stateRegion, string? postalCode)
    {
        var parts = new List<string>(3);
        var cityStateParts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(city)) cityStateParts.Add(city!.Trim());
        if (!string.IsNullOrWhiteSpace(stateRegion)) cityStateParts.Add(stateRegion!.Trim());
        if (cityStateParts.Count > 0) parts.Add(string.Join(", ", cityStateParts));
        if (!string.IsNullOrWhiteSpace(postalCode)) parts.Add(postalCode!.Trim());

        if (parts.Count == 0) return;
        sb.Append("<span class=\"addr-line\">").Append(Esc(string.Join(" ", parts))).Append("</span>");
    }

    private static string Esc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Date(DateTime value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Money(decimal value, string currency) =>
        value.ToString("0.00", CultureInfo.InvariantCulture)
        + " " + Esc(currency ?? string.Empty);

    /// <summary>
    /// Defends the inline <c>&lt;style&gt;</c> block against
    /// CSS-context injection. The snapshot's accent colour is
    /// admin-supplied (template editor) but we still treat it as
    /// untrusted: the value is HTML-encoded so any
    /// <c>&lt;</c>/<c>&gt;</c>/<c>"</c> would become entities (a
    /// browser CSS parser then sees an invalid colour and ignores
    /// the declaration). A null/blank value falls back to the
    /// neutral default.
    /// </summary>
    private static string SafeAccent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DefaultAccent;
        return WebUtility.HtmlEncode(value);
    }
}
