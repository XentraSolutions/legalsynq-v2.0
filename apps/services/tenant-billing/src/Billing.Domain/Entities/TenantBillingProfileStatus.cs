namespace Billing.Domain.Entities;

/// <summary>
/// Allowed values for <see cref="TenantBillingProfile.Status"/>. Stored as a
/// short string column (mirrors the <c>InvoiceTemplateStatus</c> pattern) so
/// the value is human-readable in MySQL queries and forward-compatible with
/// new states without an enum migration.
///
/// <list type="bullet">
///   <item><c>Draft</c> — newly recorded mapping; not yet honoured by the
///         resolver. Allows operators to stage a profile before turning it
///         on.</item>
///   <item><c>Active</c> — mapping is live; the resolver will return its
///         <see cref="TenantBillingProfile.BillingAccountId"/> for the
///         tenant.</item>
///   <item><c>Suspended</c> — temporarily paused; the resolver returns null
///         while in this state. Reversible: the operator can flip it back to
///         Active without creating a new profile.</item>
///   <item><c>Closed</c> — terminal. The mapping is retired (e.g. the tenant
///         was migrated to a different billing account). Closed rows remain
///         in the table as an audit trail; a new Active profile may now be
///         created for the same tenant.</item>
/// </list>
/// </summary>
public static class TenantBillingProfileStatus
{
    public const string Draft     = "Draft";
    public const string Active    = "Active";
    public const string Suspended = "Suspended";
    public const string Closed    = "Closed";

    public static bool IsValid(string? value)
        => value is Draft or Active or Suspended or Closed;
}
