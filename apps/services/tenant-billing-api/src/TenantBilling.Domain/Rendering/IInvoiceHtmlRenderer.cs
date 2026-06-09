namespace TenantBilling.Domain.Rendering;

/// <summary>
/// INV-TPL-03 — Pure, deterministic conversion of an
/// <see cref="InvoiceRenderDocument"/> into an HTML string. Stateless
/// (registered as singleton in DI). Implementations MUST escape every
/// user/admin-supplied text field via
/// <see cref="System.Net.WebUtility.HtmlEncode(string?)"/> and MUST
/// NOT emit external JavaScript or accept raw HTML from input.
/// </summary>
public interface IInvoiceHtmlRenderer
{
    string Render(InvoiceRenderDocument document);
}
