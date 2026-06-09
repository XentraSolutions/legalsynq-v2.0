using System.ComponentModel.DataAnnotations;
using Billing.Domain.Entities;

namespace Billing.Api.Contracts;

/// <summary>
/// TB-DATA-01 — request body for <c>POST /api/tenant-billing/profiles</c>.
/// The owning tenant is taken from the request's <c>X-Tenant-Id</c> header
/// (<c>ITenantContext</c>), never from the body.
/// </summary>
public sealed class CreateTenantBillingProfileRequest
{
    [Required]
    public Guid BillingAccountId { get; set; }

    [MaxLength(100)] public string? HostPlatformKey { get; set; }
    [MaxLength(200)] public string? ExternalTenantId { get; set; }

    /// <summary>One of <see cref="TenantBillingMode"/> values. Defaults to InternalOnly.</summary>
    [MaxLength(32)]
    public string Mode { get; set; } = TenantBillingMode.InternalOnly;

    [MaxLength(2000)] public string? Notes { get; set; }
}

public sealed record TenantBillingProfileResponse(
    Guid Id,
    Guid TenantId,
    Guid BillingAccountId,
    string? HostPlatformKey,
    string? ExternalTenantId,
    string Status,
    string Mode,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ActivatedAtUtc,
    DateTime? ClosedAtUtc)
{
    public static TenantBillingProfileResponse From(TenantBillingProfile p) => new(
        p.Id, p.TenantId, p.BillingAccountId,
        p.HostPlatformKey, p.ExternalTenantId,
        p.Status, p.Mode, p.Notes,
        p.CreatedAtUtc, p.UpdatedAtUtc,
        p.ActivatedAtUtc, p.ClosedAtUtc);
}

public sealed record TenantBillingProfileListResponse(
    IReadOnlyList<TenantBillingProfileResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
