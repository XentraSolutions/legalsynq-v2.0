namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Dispatches attachment streams to the Documents adapter.
///
/// Rules:
/// - Must not buffer the entire stream in memory.
/// - Documents adapter failure must NOT fall back to binary DB storage.
/// - Dispatch is idempotent — re-dispatching a Pending or Failed reference is safe.
/// - If the Documents adapter is unavailable, the attachment reference remains Pending.
/// - Sensitive provider attachment IDs must not appear in logs.
/// </summary>
public interface IAttachmentDispatcher
{
    /// <summary>
    /// Dispatches a single attachment to the Documents adapter.
    /// The attachment stream is fetched from the connector, streamed to Documents,
    /// and the reference is updated.
    /// </summary>
    Task<AttachmentDispatchResult> DispatchAsync(
        AttachmentDispatchRequest request,
        CancellationToken ct = default);
}

public sealed record AttachmentDispatchRequest
{
    public required Guid TenantId { get; init; }
    public required Guid AttachmentReferenceId { get; init; }
    public required Guid EmailMessageId { get; init; }
    public required string FileName { get; init; }
    public required string? MimeType { get; init; }
    public required long? MaxSizeBytes { get; init; }

    /// <summary>Opaque provider attachment ID. Never log this value.</summary>
    public required string? ProviderAttachmentId { get; init; }

    /// <summary>The provider message ID needed to fetch the attachment stream.</summary>
    public required string ProviderMessageId { get; init; }

    public required EmailSourceConnectorContext ConnectorContext { get; init; }
}

public sealed record AttachmentDispatchResult
{
    public required bool Success { get; init; }
    public Guid? DocumentReferenceId { get; init; }
    public string? ContentHash { get; init; }
    public string? ErrorCode { get; init; }
    public string? SafeErrorSummary { get; init; }
    public bool WasSkipped { get; init; }
    public string? SkipReason { get; init; }
}
