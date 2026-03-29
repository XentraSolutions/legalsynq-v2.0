using Documents.Domain.Entities;

namespace Documents.Domain.Interfaces;

/// <summary>
/// Port for the in-process scan job queue.
/// Default implementation uses System.Threading.Channels (in-memory).
/// Can be replaced with Redis Streams / AWS SQS for production-scale deployments.
/// </summary>
public interface IScanJobQueue
{
    /// <summary>Enqueue a scan job. Non-blocking unless the queue is full.</summary>
    ValueTask EnqueueAsync(ScanJob job, CancellationToken ct = default);

    /// <summary>
    /// Dequeue a scan job. Blocks until one is available or <paramref name="ct"/> is cancelled.
    /// Returns null when the channel is completed.
    /// </summary>
    ValueTask<ScanJob?> DequeueAsync(CancellationToken ct = default);

    int Count { get; }
}
