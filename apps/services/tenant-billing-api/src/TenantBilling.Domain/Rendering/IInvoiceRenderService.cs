namespace TenantBilling.Domain.Rendering;

/// <summary>
/// INV-TPL-03 — Build a render document for an invoice and emit
/// downstream-friendly representations (HTML today; PDF deferred).
/// Returns <c>null</c> when the invoice does not exist or belongs to
/// a different tenant — controllers translate that into a 404 with
/// no cross-tenant existence leak.
/// </summary>
public interface IInvoiceRenderService
{
    /// <summary>
    /// Build the structured render model for the invoice. Pure read:
    /// never mutates lifecycle state, never resolves a live template.
    /// </summary>
    Task<InvoiceRenderDocument?> BuildRenderDocumentAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Convenience: build the render document and pass it to the
    /// configured <see cref="IInvoiceHtmlRenderer"/>. Returns
    /// <c>null</c> when the invoice does not exist or belongs to
    /// a different tenant.
    /// </summary>
    Task<string?> RenderHtmlAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default);
}
