namespace Billing.Api.LegalSynq;

/// <summary>
/// LS-INT-01 — result of <see cref="ITenantIdentityContextResolver.ResolveAsync"/>.
/// </summary>
public sealed class TenantResolutionResult
{
    private TenantResolutionResult() { }

    public bool IsResolved { get; private init; }
    public Guid TenantId { get; private init; }
    public TenantResolutionSource Source { get; private init; }
    public string? FailureReason { get; private init; }

    public static TenantResolutionResult Resolved(Guid tenantId, TenantResolutionSource source)
        => new() { IsResolved = true, TenantId = tenantId, Source = source };

    public static TenantResolutionResult Failed(string reason)
        => new() { IsResolved = false, FailureReason = reason };
}

/// <summary>How the tenant was resolved.</summary>
public enum TenantResolutionSource
{
    /// <summary>Resolved from a LegalSynq JWT <c>tenant_id</c> claim.</summary>
    JwtClaim,

    /// <summary>Resolved from the <c>X-Tenant-Id</c> header (standalone / internal-service fallback).</summary>
    Header,
}
