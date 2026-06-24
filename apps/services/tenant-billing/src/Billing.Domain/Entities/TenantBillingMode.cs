namespace Billing.Domain.Entities;

/// <summary>
/// Operating mode for a <see cref="TenantBillingProfile"/> — orthogonal to
/// <see cref="TenantBillingProfileStatus"/>. Status describes whether the
/// mapping is honoured at all; mode describes how downstream flows should
/// interpret it once it is honoured.
///
/// <list type="bullet">
///   <item><c>Disabled</c> — record-only. Resolver returns the
///         BillingAccountId for read use cases but downstream charge flows
///         should treat this as "do not invoice".</item>
///   <item><c>InternalOnly</c> — the platform invoices the tenant directly;
///         the tenant has no end-customer billing of its own through this
///         profile.</item>
///   <item><c>TenantOperated</c> — the tenant runs its own billing on top of
///         the platform; the BillingAccount is for the platform-side
///         relationship only.</item>
///   <item><c>PlatformManaged</c> — the platform operates billing on the
///         tenant's behalf (managed-service tier).</item>
/// </list>
/// </summary>
public static class TenantBillingMode
{
    public const string Disabled        = "Disabled";
    public const string InternalOnly    = "InternalOnly";
    public const string TenantOperated  = "TenantOperated";
    public const string PlatformManaged = "PlatformManaged";

    public static bool IsValid(string? value)
        => value is Disabled or InternalOnly or TenantOperated or PlatformManaged;
}
