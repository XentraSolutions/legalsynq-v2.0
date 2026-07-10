using Xenia.Application.Adapters.Interfaces;

namespace Xenia.Infrastructure.Platform;

/// <summary>
/// Noop implementation of <see cref="IDocumentAdapter"/>.
/// Returns honest unavailable results. Never reports false success.
/// </summary>
internal sealed class UnavailableDocumentAdapter : IDocumentAdapter
{
    public bool IsConfigured => false;

    public Task<DocumentReservationResult?> ReserveDocumentAsync(
        Guid tenantId, string fileName, string contentType, CancellationToken ct = default)
        => Task.FromResult<DocumentReservationResult?>(
            new DocumentReservationResult(Guid.Empty, string.Empty, IsAvailable: false));

    public Task<DocumentMetadataResult?> GetDocumentMetadataAsync(
        Guid tenantId, Guid documentId, CancellationToken ct = default)
        => Task.FromResult<DocumentMetadataResult?>(null);
}
