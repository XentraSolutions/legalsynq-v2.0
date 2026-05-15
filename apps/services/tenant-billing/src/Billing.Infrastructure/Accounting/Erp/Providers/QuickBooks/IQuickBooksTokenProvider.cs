namespace Billing.Infrastructure.Accounting.Erp.Providers.QuickBooks;

/// <summary>
/// MS-BILL-ERP-002 — Acquires a short-lived QuickBooks Online
/// access token. Implementations cache the token in-process and
/// transparently refresh it via the configured refresh token
/// before expiry.
///
/// <para>
/// The contract intentionally does NOT expose the refresh token
/// or any persistence-layer side effect. The access token is the
/// ONLY value ever returned, and only to in-process callers
/// (the provider). It is never logged, never echoed to the
/// browser, and never persisted.
/// </para>
/// </summary>
public interface IQuickBooksTokenProvider
{
    /// <summary>
    /// Returns a non-empty bearer access token for the configured
    /// QuickBooks Online realm, refreshing it as needed. Throws
    /// <see cref="QuickBooksTokenException"/> on any unrecoverable
    /// auth failure (missing config, refresh rejected, transport
    /// error). Callers MUST translate the exception into a
    /// deterministic provider result — never re-throw to the
    /// controller.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}

/// <summary>
/// MS-BILL-ERP-002 — Sentinel raised by
/// <see cref="IQuickBooksTokenProvider"/> when an access token
/// cannot be obtained. The <see cref="Reason"/> is a short,
/// NON-PII tag (<c>"NotConfigured"</c>, <c>"RefreshRejected"</c>,
/// <c>"TokenEndpointTransport"</c>, <c>"MalformedTokenResponse"</c>)
/// safe to surface in the operator UI as the FailureReason.
/// </summary>
public sealed class QuickBooksTokenException : System.Exception
{
    public string Reason { get; }

    public QuickBooksTokenException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public QuickBooksTokenException(string reason, string message, System.Exception inner)
        : base(message, inner)
    {
        Reason = reason;
    }
}
