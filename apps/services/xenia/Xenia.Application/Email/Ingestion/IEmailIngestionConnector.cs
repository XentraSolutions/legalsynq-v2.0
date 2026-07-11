namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Extended connector contract for email ingestion operations.
///
/// Extends the base validation contract with message fetching capabilities.
/// Provider connectors that support ingestion must implement this interface in addition
/// to <see cref="IEmailSourceConnector"/>.
///
/// Connectors must:
/// - Never buffer the full mailbox in memory.
/// - Return messages in stable chronological or UID order.
/// - Respect cancellation tokens.
/// - Redact all sensitive fields from returned envelopes.
/// - Not expose raw cursors in returned data.
/// </summary>
public interface IEmailIngestionConnector
{
    /// <summary>Returns the ingestion capabilities for this connector/environment.</summary>
    ProviderSyncCapabilities GetIngestionCapabilities();

    /// <summary>
    /// Obtains the initial cursor for a full sync.
    /// For IMAP: returns UIDVALIDITY + starting UID.
    /// For Google: returns starting historyId.
    /// For M365: returns an empty delta token page URL.
    /// </summary>
    Task<ProviderInitialCursorResult> GetInitialCursorAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Fetches one page of messages from the provider.
    /// Cursor may be null for the very first page of an initial sync.
    /// </summary>
    Task<ProviderFetchPageResult> FetchMessagePageAsync(
        EmailSourceConnectorContext context,
        ProviderSyncCursor? cursor,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the attachment stream for a specific attachment.
    /// The stream must be consumed and closed by the caller.
    /// Returns null if unavailable.
    /// </summary>
    Task<Stream?> GetAttachmentStreamAsync(
        EmailSourceConnectorContext context,
        string providerMessageId,
        string providerAttachmentId,
        CancellationToken ct = default);
}
