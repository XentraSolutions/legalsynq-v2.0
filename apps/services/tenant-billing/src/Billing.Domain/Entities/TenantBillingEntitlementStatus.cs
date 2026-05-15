namespace Billing.Domain.Entities;

/// <summary>
/// TB-DATA-02 — allowed values for
/// <see cref="TenantBillingEntitlementSnapshot.EntitlementStatus"/>.
/// Stored as a short string (mirrors <see cref="TenantBillingProfileStatus"/>).
///
/// <list type="bullet">
///   <item><c>Unknown</c> — no decisive signal yet (initial / cleared).</item>
///   <item><c>Enabled</c> — the source system says the tenant should be able
///         to use Tenant Billing (subject to <c>AccessRecommendation</c>).</item>
///   <item><c>Disabled</c> — the source system says NO; reasons typically
///         include cancellation, plan downgrade, etc.</item>
///   <item><c>Suspended</c> — temporarily blocked (e.g. PastDue / dunning).</item>
///   <item><c>Expired</c> — the underlying subscription/term has lapsed.</item>
/// </list>
/// </summary>
public static class TenantBillingEntitlementStatus
{
    public const string Unknown   = "Unknown";
    public const string Enabled   = "Enabled";
    public const string Disabled  = "Disabled";
    public const string Suspended = "Suspended";
    public const string Expired   = "Expired";

    public static bool IsValid(string? value)
        => value is Unknown or Enabled or Disabled or Suspended or Expired;
}
