using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;

namespace Identity.Tests;

internal sealed class NoOpAuditEventClient : IAuditEventClient
{
    public Task<IngestResult> IngestAsync(
        IngestAuditEventRequest request,
        CancellationToken ct = default) =>
        Task.FromResult(new IngestResult(true, Guid.NewGuid().ToString(), null, 200));

    public Task<BatchIngestResult> IngestBatchAsync(
        BatchIngestRequest request,
        CancellationToken ct = default) =>
        Task.FromResult(new BatchIngestResult(0, 0, 0, Array.Empty<IngestResult>()));
}
