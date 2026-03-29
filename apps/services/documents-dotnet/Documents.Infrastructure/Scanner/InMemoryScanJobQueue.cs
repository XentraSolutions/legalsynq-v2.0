using System.Threading.Channels;
using Documents.Domain.Entities;
using Documents.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Documents.Infrastructure.Scanner;

/// <summary>
/// In-process, bounded Channel-based scan job queue.
/// Thread-safe for concurrent producers (API handlers) and a single consumer (background worker).
/// Can be replaced with a Redis Streams or SQS adapter by swapping the IScanJobQueue registration.
/// </summary>
public sealed class InMemoryScanJobQueue : IScanJobQueue
{
    private readonly Channel<ScanJob>            _channel;
    private readonly ILogger<InMemoryScanJobQueue> _log;

    public int Count => _channel.Reader.Count;

    public InMemoryScanJobQueue(
        ILogger<InMemoryScanJobQueue> log,
        int capacity = 1_000)
    {
        _log = log;

        var opts = new BoundedChannelOptions(capacity)
        {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        };

        _channel = Channel.CreateBounded<ScanJob>(opts);
    }

    public async ValueTask EnqueueAsync(ScanJob job, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(job, ct);
        _log.LogDebug("ScanQueue: enqueued job for Document={DocId} Version={VersionId}",
            job.DocumentId, job.VersionId);
    }

    public async ValueTask<ScanJob?> DequeueAsync(CancellationToken ct = default)
    {
        try
        {
            return await _channel.Reader.ReadAsync(ct);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
