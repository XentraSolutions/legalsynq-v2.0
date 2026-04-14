using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;

namespace Reports.Infrastructure.Adapters;

public sealed class MockDocumentAdapter : IDocumentAdapter
{
    private readonly ILogger<MockDocumentAdapter> _log;

    public MockDocumentAdapter(ILogger<MockDocumentAdapter> log) => _log = log;

    public Task<string> StoreReportAsync(string tenantId, string fileName, byte[] content, string mimeType, CancellationToken ct)
    {
        _log.LogDebug("MockDocumentAdapter: StoreReport {FileName} ({Bytes} bytes)", fileName, content.Length);
        return Task.FromResult($"mock-doc-{Guid.NewGuid():N}");
    }

    public Task<byte[]?> RetrieveReportAsync(string documentId, CancellationToken ct)
    {
        _log.LogDebug("MockDocumentAdapter: RetrieveReport {DocumentId}", documentId);
        return Task.FromResult<byte[]?>(null);
    }
}
