using System.ComponentModel.DataAnnotations;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;

namespace TenantBilling.Api.Contracts;

public sealed class CreateCustomerRequest
{
    // TenantId is sourced from the X-Tenant-Id request header, not the body,
    // so a single request cannot disagree with itself about the owning tenant.
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string Email { get; set; } = string.Empty;
    [MaxLength(50)] public string? Phone { get; set; }

    /// <summary>Legacy single-line billing address. Kept for callers
    /// that haven't moved to the structured fields below.</summary>
    [MaxLength(1000)] public string? BillingAddress { get; set; }

    // ---- INV-TPL-04: structured billing address ----
    [MaxLength(250)] public string? BillingAddressLine1 { get; set; }
    [MaxLength(250)] public string? BillingAddressLine2 { get; set; }
    [MaxLength(100)] public string? BillingCity { get; set; }
    [MaxLength(100)] public string? BillingStateRegion { get; set; }
    [MaxLength(100)] public string? BillingPostalCode { get; set; }
    [MaxLength(100)] public string? BillingCountry { get; set; }

    [MaxLength(200)] public string? ExternalReference { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }

    /// <summary>
    /// Bundle the structured address into a service-layer record. Used
    /// by the controller when calling <see cref="ICustomerService.CreateAsync"/>.
    /// </summary>
    public CustomerBillingAddress ToBillingAddressDetails() => new(
        BillingAddressLine1,
        BillingAddressLine2,
        BillingCity,
        BillingStateRegion,
        BillingPostalCode,
        BillingCountry);
}

public sealed class UpdateCustomerRequest
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string Email { get; set; } = string.Empty;
    [MaxLength(50)] public string? Phone { get; set; }

    [MaxLength(1000)] public string? BillingAddress { get; set; }

    [MaxLength(250)] public string? BillingAddressLine1 { get; set; }
    [MaxLength(250)] public string? BillingAddressLine2 { get; set; }
    [MaxLength(100)] public string? BillingCity { get; set; }
    [MaxLength(100)] public string? BillingStateRegion { get; set; }
    [MaxLength(100)] public string? BillingPostalCode { get; set; }
    [MaxLength(100)] public string? BillingCountry { get; set; }

    [MaxLength(200)] public string? ExternalReference { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }

    public CustomerBillingAddress ToBillingAddressDetails() => new(
        BillingAddressLine1,
        BillingAddressLine2,
        BillingCity,
        BillingStateRegion,
        BillingPostalCode,
        BillingCountry);
}

public sealed record CustomerResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Email,
    string? Phone,
    string? BillingAddress,
    string? BillingAddressLine1,
    string? BillingAddressLine2,
    string? BillingCity,
    string? BillingStateRegion,
    string? BillingPostalCode,
    string? BillingCountry,
    string? ExternalReference,
    string? Notes,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CustomerResponse From(Customer c) => new(
        c.Id, c.TenantId, c.Name, c.Email, c.Phone, c.BillingAddress,
        c.BillingAddressLine1, c.BillingAddressLine2, c.BillingCity,
        c.BillingStateRegion, c.BillingPostalCode, c.BillingCountry,
        c.ExternalReference, c.Notes, c.IsDeleted, c.CreatedAt, c.UpdatedAt);
}

public sealed record CustomerListResponse(
    IReadOnlyList<CustomerResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
