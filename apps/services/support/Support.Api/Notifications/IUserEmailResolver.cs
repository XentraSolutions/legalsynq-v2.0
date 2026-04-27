namespace Support.Api.Notifications;

/// <summary>
/// Resolves the email address for a platform user identified by their user ID.
/// Used at notification-publish time so that admin / assigned-user recipients
/// can be addressed by email without requiring the caller to hold the address.
/// </summary>
public interface IUserEmailResolver
{
    /// <summary>
    /// Returns the email address for <paramref name="userId"/> within
    /// <paramref name="tenantId"/>, or <c>null</c> when the user is not found
    /// or the identity DB is unavailable.  Failures are swallowed — the
    /// notification pipeline must not break on a missing email.
    /// </summary>
    Task<string?> ResolveAsync(string userId, string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Batch-resolves emails for a set of user IDs scoped to a single tenant.
    /// Returns a dictionary keyed by userId; absent entries mean "not found".
    /// </summary>
    Task<Dictionary<string, string>> ResolveManyAsync(IEnumerable<string> userIds, string tenantId, CancellationToken ct = default);
}
