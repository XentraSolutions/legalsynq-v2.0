using System.Globalization;
using System.Net;
using System.Text;

namespace TenantBilling.Domain.Statements;

/// <summary>
/// Default <see cref="ICustomerStatementHtmlRenderer"/>.
///
/// Stateless / pure. Produces a single self-contained HTML document
/// from a render model. No external CSS, no JavaScript, no network
/// references at all.
///
/// Visual style: a clean accountancy-style statement on a white
/// "sheet" with a soft grey page background, a fixed neutral accent
/// (no per-template branding in STAT-B01 — see report §10), a summary
/// box on the right of the header, a transactions table with a
/// running balance column, and an outstanding-invoices table at the
/// bottom.
///
/// Security model:
/// <list type="bullet">
///   <item>Every text field originating from user/admin/customer
///     input (customer name/email, invoice number, payment method,
///     transaction reference, status string) is escaped via
///     <see cref="WebUtility.HtmlEncode(string?)"/> before being
///     written to the document.</item>
///   <item>No JavaScript is emitted; a literal <c>&lt;script&gt;</c>
///     in any input renders as visible text.</item>
///   <item>All CSS is inlined in a single <c>&lt;style&gt;</c> block
///     hard-coded by the renderer — no user input is interpolated
///     into the CSS.</item>
/// </list>
/// </summary>
public sealed class CustomerStatementHtmlRenderer : ICustomerStatementHtmlRenderer
{
    private const string Accent = "#1f2937";

    public string Render(CustomerStatementDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sb = new StringBuilder(4096);

        sb.Append("<!doctype html><html lang=\"en\"><head>");
        sb.Append("<meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<title>Statement for ").Append(Esc(document.CustomerName)).Append("</title>");
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("body{font-family:-apple-system,BlinkMacSystemFont,\"Segoe UI\",Roboto,\"Helvetica Neue\",Arial,sans-serif;");
        sb.Append("color:#393A3D;background:#F4F5F8;margin:0;padding:0;-webkit-font-smoothing:antialiased;}");
        sb.Append(".sheet{max-width:880px;margin:24px auto;background:#fff;");
        sb.Append("padding:48px 56px;box-shadow:0 1px 4px rgba(0,0,0,.08);border-radius:2px;}");
        // Header band
        sb.Append(".stmt-header{display:flex;justify-content:space-between;align-items:flex-start;gap:32px;margin-bottom:32px;}");
        sb.Append(".id-block{flex:1;min-width:0;}");
        sb.Append(".id-block .biz-name{font-size:18px;font-weight:600;color:#393A3D;margin:0 0 4px;}");
        sb.Append(".id-block p{margin:0;color:#393A3D;font-size:13px;line-height:1.5;}");
        sb.Append(".mark-block{flex:0 0 auto;text-align:right;}");
        sb.Append(".mark{font-size:30px;font-weight:300;letter-spacing:5px;color:#6B6C72;text-transform:uppercase;line-height:1;}");
        sb.Append(".stmt-num{font-size:11px;color:#6B6C72;margin-top:8px;letter-spacing:.4px;}");
        // Meta strip
        sb.Append(".stmt-meta{display:flex;justify-content:flex-end;margin-bottom:24px;}");
        sb.Append(".meta-grid{display:inline-grid;grid-template-columns:auto auto;gap:6px 18px;font-size:12px;}");
        sb.Append(".meta-grid .k{color:#6B6C72;text-transform:uppercase;letter-spacing:.6px;font-weight:600;}");
        sb.Append(".meta-grid .v{color:#393A3D;font-weight:500;text-align:right;}");
        // Summary box
        sb.Append(".summary{display:grid;grid-template-columns:repeat(5,1fr);gap:0;margin:0 0 32px;");
        sb.Append("border:1px solid #E1E4E8;border-radius:4px;overflow:hidden;}");
        sb.Append(".summary .cell{padding:12px 14px;border-right:1px solid #E1E4E8;}");
        sb.Append(".summary .cell:last-child{border-right:none;background:").Append(Accent).Append(";color:#fff;}");
        sb.Append(".summary .label{display:block;font-size:10px;text-transform:uppercase;letter-spacing:.8px;");
        sb.Append("color:#6B6C72;margin-bottom:6px;font-weight:600;}");
        sb.Append(".summary .cell:last-child .label{color:#cfd6e0;}");
        sb.Append(".summary .value{display:block;font-size:15px;font-weight:600;color:#393A3D;}");
        sb.Append(".summary .cell:last-child .value{color:#fff;}");
        // Bill-to + period
        sb.Append(".parties{display:flex;gap:48px;margin-bottom:24px;}");
        sb.Append(".parties .party{flex:1;min-width:0;}");
        sb.Append(".party .label{font-size:11px;color:#6B6C72;text-transform:uppercase;letter-spacing:1.2px;");
        sb.Append("font-weight:600;display:block;margin-bottom:8px;border-bottom:1px solid #E1E4E8;padding-bottom:6px;}");
        sb.Append(".party .row{display:block;font-size:13px;color:#393A3D;line-height:1.65;}");
        sb.Append(".party .row.headline{font-weight:600;}");
        // Tables
        sb.Append("h3.section-title{font-size:11px;color:#6B6C72;text-transform:uppercase;");
        sb.Append("letter-spacing:1.2px;font-weight:600;margin:24px 0 8px;}");
        sb.Append("table.t{width:100%;border-collapse:collapse;margin:0 0 12px;}");
        sb.Append("table.t th{background:#F4F5F8;color:#393A3D;text-transform:uppercase;");
        sb.Append("letter-spacing:.6px;font-size:11px;font-weight:600;padding:10px 12px;");
        sb.Append("border-bottom:2px solid ").Append(Accent).Append(";text-align:left;}");
        sb.Append("table.t td{padding:10px 12px;border-bottom:1px solid #E1E4E8;font-size:13px;color:#393A3D;}");
        sb.Append("table.t .text-right{text-align:right;}");
        sb.Append("table.t .empty td{padding:18px 12px;color:#6B6C72;font-style:italic;text-align:center;}");
        sb.Append("table.t .opening td,table.t .closing td{background:#F4F5F8;font-weight:600;}");
        // Footer
        sb.Append(".footer{margin-top:24px;padding-top:14px;border-top:1px solid #E1E4E8;");
        sb.Append("font-size:11px;color:#6B6C72;text-align:center;}");
        sb.Append("@media print{body{background:#fff;}.sheet{box-shadow:none;margin:0;padding:24px;max-width:none;}}");
        sb.Append("</style></head><body>");

        sb.Append("<div class=\"sheet\">");

        // Header
        sb.Append("<div class=\"stmt-header\">");
        sb.Append("<div class=\"id-block\">");
        sb.Append("<div class=\"biz-name\">Customer statement</div>");
        sb.Append("<p>Period covered by this statement is shown to the right.</p>");
        sb.Append("</div>");
        sb.Append("<div class=\"mark-block\">");
        sb.Append("<div class=\"mark\">Statement</div>");
        sb.Append("<div class=\"stmt-num\">#").Append(Esc(document.StatementId.ToString("N").Substring(0, 12))).Append("</div>");
        sb.Append("</div>");
        sb.Append("</div>");

        // Meta strip
        sb.Append("<div class=\"stmt-meta\"><div class=\"meta-grid\">");
        sb.Append("<div class=\"k\">Period</div><div class=\"v\">")
            .Append(Date(document.PeriodStartDate)).Append(" – ").Append(Date(document.PeriodEndDate))
            .Append("</div>");
        sb.Append("<div class=\"k\">Generated</div><div class=\"v\">")
            .Append(document.GeneratedAtUtc.ToString("u", CultureInfo.InvariantCulture))
            .Append("</div>");
        sb.Append("<div class=\"k\">Currency</div><div class=\"v\">").Append(Esc(document.Currency)).Append("</div>");
        sb.Append("</div></div>");

        // Bill-to
        sb.Append("<div class=\"parties\"><div class=\"party\">");
        sb.Append("<span class=\"label\">Statement for</span>");
        sb.Append("<span class=\"row headline\">")
          .Append(Esc(string.IsNullOrWhiteSpace(document.CustomerName) ? "(customer)" : document.CustomerName))
          .Append("</span>");
        if (!string.IsNullOrWhiteSpace(document.CustomerEmail))
        {
            sb.Append("<span class=\"row\">").Append(Esc(document.CustomerEmail)).Append("</span>");
        }
        sb.Append("</div></div>");

        // Summary box (5 cells: opening, invoiced, paid, closing, outstanding)
        sb.Append("<div class=\"summary\">");
        AppendSummaryCell(sb, "Opening balance", Money(document.OpeningBalance, document.Currency));
        AppendSummaryCell(sb, "Total invoiced", Money(document.TotalInvoiced, document.Currency));
        AppendSummaryCell(sb, "Total paid", Money(document.TotalPaid, document.Currency));
        AppendSummaryCell(sb, "Closing balance", Money(document.ClosingBalance, document.Currency));
        AppendSummaryCell(sb, "Outstanding", Money(document.OutstandingBalance, document.Currency));
        sb.Append("</div>");

        // Transactions table
        sb.Append("<h3 class=\"section-title\">Transactions</h3>");
        sb.Append("<table class=\"t\"><thead><tr>");
        sb.Append("<th>Date</th><th>Type</th><th>Reference</th><th>Description</th>");
        sb.Append("<th class=\"text-right\">Charge</th><th class=\"text-right\">Payment</th>");
        sb.Append("<th class=\"text-right\">Balance</th>");
        sb.Append("</tr></thead><tbody>");
        // Opening row anchors the running balance.
        sb.Append("<tr class=\"opening\"><td>").Append(Date(document.PeriodStartDate)).Append("</td>");
        sb.Append("<td>Opening</td><td>—</td><td>Opening balance</td>");
        sb.Append("<td class=\"text-right\">—</td><td class=\"text-right\">—</td>");
        sb.Append("<td class=\"text-right\">").Append(Money(document.OpeningBalance, document.Currency)).Append("</td></tr>");

        if (document.Transactions.Count == 0)
        {
            sb.Append("<tr class=\"empty\"><td colspan=\"7\">No transactions in this period.</td></tr>");
        }
        else
        {
            foreach (var t in document.Transactions)
            {
                sb.Append("<tr><td>").Append(Date(t.TransactionDate)).Append("</td>");
                sb.Append("<td>").Append(t.Type == CustomerStatementTransactionType.Invoice ? "Invoice" : "Payment").Append("</td>");
                sb.Append("<td>").Append(Esc(t.ReferenceNumber ?? "—")).Append("</td>");
                sb.Append("<td>").Append(Esc(t.Description)).Append("</td>");
                sb.Append("<td class=\"text-right\">")
                  .Append(t.DebitAmount > 0m ? Money(t.DebitAmount, document.Currency) : "—")
                  .Append("</td>");
                sb.Append("<td class=\"text-right\">")
                  .Append(t.CreditAmount > 0m ? Money(t.CreditAmount, document.Currency) : "—")
                  .Append("</td>");
                sb.Append("<td class=\"text-right\">").Append(Money(t.RunningBalance, document.Currency)).Append("</td></tr>");
            }
        }

        // Closing row mirrors the opening row at the bottom.
        sb.Append("<tr class=\"closing\"><td>").Append(Date(document.PeriodEndDate)).Append("</td>");
        sb.Append("<td>Closing</td><td>—</td><td>Closing balance</td>");
        sb.Append("<td class=\"text-right\">—</td><td class=\"text-right\">—</td>");
        sb.Append("<td class=\"text-right\">").Append(Money(document.ClosingBalance, document.Currency)).Append("</td></tr>");
        sb.Append("</tbody></table>");

        // Outstanding invoices table
        sb.Append("<h3 class=\"section-title\">Outstanding invoices</h3>");
        sb.Append("<table class=\"t\"><thead><tr>");
        sb.Append("<th>Invoice</th><th>Issued</th><th>Due</th><th>Status</th>");
        sb.Append("<th class=\"text-right\">Total</th><th class=\"text-right\">Paid</th>");
        sb.Append("<th class=\"text-right\">Balance</th><th class=\"text-right\">Days past due</th>");
        sb.Append("</tr></thead><tbody>");
        if (document.OutstandingInvoices.Count == 0)
        {
            sb.Append("<tr class=\"empty\"><td colspan=\"8\">No outstanding invoices.</td></tr>");
        }
        else
        {
            foreach (var o in document.OutstandingInvoices)
            {
                sb.Append("<tr><td>").Append(Esc(o.InvoiceNumber)).Append("</td>");
                sb.Append("<td>").Append(Date(o.IssueDate)).Append("</td>");
                sb.Append("<td>").Append(Date(o.DueDate)).Append("</td>");
                sb.Append("<td>").Append(Esc(o.Status)).Append("</td>");
                sb.Append("<td class=\"text-right\">").Append(Money(o.TotalAmount, o.Currency)).Append("</td>");
                sb.Append("<td class=\"text-right\">").Append(Money(o.AmountPaid, o.Currency)).Append("</td>");
                sb.Append("<td class=\"text-right\">").Append(Money(o.AmountDue, o.Currency)).Append("</td>");
                sb.Append("<td class=\"text-right\">").Append(o.DaysPastDue.ToString(CultureInfo.InvariantCulture)).Append("</td></tr>");
            }
        }
        sb.Append("</tbody></table>");

        sb.Append("<div class=\"footer\">Generated ")
          .Append(document.GeneratedAtUtc.ToString("u", CultureInfo.InvariantCulture))
          .Append("</div>");

        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static void AppendSummaryCell(StringBuilder sb, string label, string value)
    {
        sb.Append("<div class=\"cell\">");
        sb.Append("<span class=\"label\">").Append(Esc(label)).Append("</span>");
        sb.Append("<span class=\"value\">").Append(value).Append("</span>");
        sb.Append("</div>");
    }

    private static string Esc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Date(DateTime value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Money(decimal value, string currency) =>
        value.ToString("0.00", CultureInfo.InvariantCulture)
        + " " + Esc(currency ?? string.Empty);
}
