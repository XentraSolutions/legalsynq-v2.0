using Xenia.Application.Email;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Pass-through that makes existing connectors implement IEmailIngestionConnector
/// without requiring real provider connectivity.
/// Returns an empty page (no messages). Used when no live provider connectivity is available.
/// </summary>
internal sealed class NoopEmailIngestionConnector : IEmailIngestionConnector
{
    private readonly EmailProviderType _providerType;

    public NoopEmailIngestionConnector(EmailProviderType providerType) =>
        _providerType = providerType;

    public ProviderSyncCapabilities GetIngestionCapabilities() =>
        new()
        {
            ProviderType        = _providerType,
            CanFetchMessages    = false,
            CanFetchAttachments = false,
            SupportsDeltaSync   = false,
            SupportsCancel      = false,
            UnavailableReason   = "No live provider connectivity configured in this environment.",
        };

    public Task<ProviderInitialCursorResult> GetInitialCursorAsync(
        EmailSourceConnectorContext context, CancellationToken ct = default) =>
        Task.FromResult(new ProviderInitialCursorResult
        {
            Success       = true,
            Cursor        = new ProviderSyncCursor
            {
                CursorType  = SyncCursorType.DeltaToken,
                RawValue    = "noop-initial",
                SafeSummary = "Noop initial cursor",
            },
        });

    public Task<ProviderFetchPageResult> FetchMessagePageAsync(
        EmailSourceConnectorContext context,
        ProviderSyncCursor? cursor,
        int pageSize,
        CancellationToken ct = default) =>
        Task.FromResult(new ProviderFetchPageResult
        {
            Success = true,
            Page    = new ProviderSyncPage
            {
                Messages   = [],
                NextCursor = null,
            },
        });

    public Task<Stream?> GetAttachmentStreamAsync(
        EmailSourceConnectorContext context,
        string providerMessageId,
        string providerAttachmentId,
        CancellationToken ct = default) =>
        Task.FromResult<Stream?>(null);
}
