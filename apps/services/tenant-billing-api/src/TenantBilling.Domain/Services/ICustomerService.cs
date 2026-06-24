using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Services;

/// <summary>
/// INV-TPL-04 — bundle of structured billing-address fields passed
/// through to <see cref="ICustomerService.CreateAsync"/> /
/// <see cref="ICustomerService.UpdateAsync"/>. Captured as a single
/// optional record (rather than 6 separate trailing parameters) so
/// existing positional callers keep compiling and the address shape
/// can evolve without further signature changes.
/// </summary>
public sealed record CustomerBillingAddress(
    string? Line1 = null,
    string? Line2 = null,
    string? City = null,
    string? StateRegion = null,
    string? PostalCode = null,
    string? Country = null)
{
    /// <summary>
    /// True when every address field is null/blank. Used by the
    /// service layer to decide whether to write null columns or
    /// attempt to normalize values.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Line1)
        && string.IsNullOrWhiteSpace(Line2)
        && string.IsNullOrWhiteSpace(City)
        && string.IsNullOrWhiteSpace(StateRegion)
        && string.IsNullOrWhiteSpace(PostalCode)
        && string.IsNullOrWhiteSpace(Country);
}

public interface ICustomerService
{
    /// <summary>
    /// Default page size when callers do not specify one.
    /// </summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Maximum page size; larger requests are clamped to this value.
    /// </summary>
    public const int MaxPageSize = 100;

    Task<Customer> CreateAsync(
        Guid tenantId,
        string name,
        string email,
        string? phone,
        string? billingAddress,
        string? externalReference,
        string? notes,
        CustomerBillingAddress? billingAddressDetails = null,
        CancellationToken ct = default);

    Task<Customer?> UpdateAsync(
        Guid tenantId,
        Guid customerId,
        string name,
        string email,
        string? phone,
        string? billingAddress,
        string? externalReference,
        string? notes,
        CustomerBillingAddress? billingAddressDetails = null,
        CancellationToken ct = default);

    Task<Customer?> GetAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);

    Task<CustomerPage> ListAsync(
        Guid tenantId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
}

public sealed record CustomerPage(
    IReadOnlyList<Customer> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
