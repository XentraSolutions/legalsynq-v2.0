using Commerce.Contracts.Invoicing;

namespace Commerce.Application.Invoicing.Abstractions;

/// <summary>
/// Reads and writes the singleton issuer-branding row applied to every
/// invoice rendered in the admin UI. <see cref="GetAsync"/> auto-creates an
/// empty default on first read so callers never see a 404.
/// </summary>
public interface IInvoiceBrandingService
{
    Task<InvoiceBrandingResponse> GetAsync(CancellationToken ct);
    Task<InvoiceBrandingResponse> UpdateAsync(UpdateInvoiceBrandingRequest request, CancellationToken ct);
}
