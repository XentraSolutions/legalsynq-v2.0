namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Protects opaque provider cursor values at rest.
///
/// Cursor values (delta tokens, history IDs, UID strings) must never be stored
/// in plain text, exposed through APIs, written to logs, or included in audit events.
///
/// The protector binds each protected value to a (TenantId, SourceId) pair to prevent
/// cursor values being swapped between sources or tenants.
///
/// Protection failure (wrong key, wrong binding, tampering) returns null/throws — it
/// must never advance the cursor or allow ingestion to continue.
/// </summary>
public interface IProviderCursorProtector
{
    /// <summary>Returns the current protection version string (e.g. "v1").</summary>
    string GetVersion();

    /// <summary>
    /// Protects a raw cursor value, binding it to the given tenant and source.
    /// The protected string is safe to persist in the database.
    /// </summary>
    Task<string> ProtectAsync(string rawCursor, Guid tenantId, Guid emailSourceId, CancellationToken ct = default);

    /// <summary>
    /// Unprotects a previously protected cursor value, verifying the tenant/source binding.
    /// Returns null if the value is invalid, tampered, or bound to a different tenant/source.
    /// Must never throw — failures indicate invalid state and must stop cursor advancement.
    /// </summary>
    Task<string?> UnprotectAsync(string protectedCursor, Guid tenantId, Guid emailSourceId, CancellationToken ct = default);
}
