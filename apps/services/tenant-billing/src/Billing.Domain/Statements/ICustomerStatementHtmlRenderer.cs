namespace Billing.Domain.Statements;

/// <summary>
/// STAT-B01 — Pure, deterministic conversion of a
/// <see cref="CustomerStatementDocument"/> into a self-contained HTML
/// string. Stateless (registered as singleton in DI). Implementations
/// MUST escape every user/admin-supplied text field via
/// <see cref="System.Net.WebUtility.HtmlEncode(string?)"/> and MUST
/// NOT emit external scripts, external CSS, or accept raw HTML from
/// input.
/// </summary>
public interface ICustomerStatementHtmlRenderer
{
    string Render(CustomerStatementDocument document);
}
