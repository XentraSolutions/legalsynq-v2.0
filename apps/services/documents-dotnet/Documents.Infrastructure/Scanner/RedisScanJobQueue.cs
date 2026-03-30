using Documents.Domain.Entities;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace Documents.Infrastructure.Scanner;

/// <summary>
/// Durable Redis Streams-backed scan job queue.
/// Provides at-least-once delivery using consumer groups:
///   - XADD to enqueue
///   - XREADGROUP to consume with delivery tracking (PEL)
///   - XACK + XDEL to acknowledge
///   - XAUTOCLAIM to recover stale jobs from crashed consumers
///
/// Jobs survive service restarts. Multiple worker instances can share
/// the same consumer group safely.
/// </summary>
public sealed class RedisScanJobQueue : IScanJobQueue, IDisposable
{
    private readonly IConnectionMultiplexer        _redis;
    private readonly ScanWorkerOptions             _opts;
    private readonly ILogger<RedisScanJobQueue>    _log;
    private readonly string _streamKey;
    private readonly string _groupName;

    private bool _groupEnsured;

    public int Count
    {
        get
        {
            try { return (int)_redis.GetDatabase().StreamLength(_streamKey); }
            catch { return -1; }
        }
    }

    public RedisScanJobQueue(
        IConnectionMultiplexer      redis,
        IOptions<ScanWorkerOptions> opts,
        ILogger<RedisScanJobQueue>  log)
    {
        _redis     = redis;
        _opts      = opts.Value;
        _log       = log;
        _streamKey = _opts.StreamKey;
        _groupName = _opts.ConsumerGroup;
    }

    // ── Enqueue ──────────────────────────────────────────────────────────────

    public async ValueTask<bool> TryEnqueueAsync(ScanJob job, CancellationToken ct = default)
    {
        await EnsureConsumerGroupAsync();

        try
        {
            var db = _redis.GetDatabase();
            var fields = SerializeJob(job);

            var msgId = await db.StreamAddAsync(
                _streamKey,
                fields,
                maxLength: _opts.StreamMaxLength > 0 ? _opts.StreamMaxLength : null,
                useApproximateMaxLength: true);

            _log.LogDebug("RedisScanQueue: XADD {StreamKey} {MsgId} Document={DocId}",
                _streamKey, msgId, job.DocumentId);

            var enqueued = msgId != RedisValue.Null;
            if (enqueued)
            {
                ScanMetrics.ScanJobsEnqueued.Inc();
                ScanMetrics.ScanQueueDepth.Set(Count);
            }
            return enqueued;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "RedisScanQueue: XADD failed for Document={DocId}", job.DocumentId);
            ScanMetrics.ScanQueueSaturations.Inc();
            Observability.RedisMetrics.RedisConnectionFailures.Inc();
            return false;
        }
    }

    // ── Dequeue ──────────────────────────────────────────────────────────────

    public async ValueTask<ScanJobLease?> DequeueAsync(string consumerId, CancellationToken ct = default)
    {
        await EnsureConsumerGroupAsync();

        var db = _redis.GetDatabase();

        // 1. First try to reclaim stale/pending messages from crashed consumers
        var staleMs = (long)TimeSpan.FromSeconds(_opts.ClaimStaleJobsAfterSeconds).TotalMilliseconds;
        var claimed = await db.StreamAutoClaimAsync(_streamKey, _groupName, consumerId, staleMs, "0-0", count: 1);

        if (claimed.ClaimedEntries is { Length: > 0 } claimedEntries)
        {
            var entry = claimedEntries[0];
            var job   = DeserializeJob(entry);
            if (job is not null)
            {
                _log.LogInformation("RedisScanQueue: reclaimed stale job {MsgId} Document={DocId} Attempt={Attempt}",
                    entry.Id, job.DocumentId, job.AttemptCount);
                Observability.RedisMetrics.RedisStreamReclaims.Inc();
                return new ScanJobLease { Job = job, MessageId = entry.Id.ToString() };
            }
        }

        // 2. Block waiting for new messages (5 second poll interval to check cancellation)
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var entries = await db.StreamReadGroupAsync(
                    _streamKey, _groupName, consumerId,
                    position: ">",
                    count: 1,
                    noAck: false);

                if (entries is { Length: > 0 })
                {
                    var entry = entries[0];
                    var job   = DeserializeJob(entry);
                    if (job is not null)
                    {
                        _log.LogDebug("RedisScanQueue: XREADGROUP {MsgId} Document={DocId}",
                            entry.Id, job.DocumentId);
                        return new ScanJobLease { Job = job, MessageId = entry.Id.ToString() };
                    }

                    // Malformed entry — acknowledge and skip
                    await db.StreamAcknowledgeAsync(_streamKey, _groupName, entry.Id);
                    continue;
                }

                // No new messages — wait briefly then loop
                await Task.Delay(2_000, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "RedisScanQueue: XREADGROUP error");
                Observability.RedisMetrics.RedisConnectionFailures.Inc();
                await Task.Delay(3_000, ct);
            }
        }

        return null;
    }

    // ── Acknowledge ──────────────────────────────────────────────────────────

    public async ValueTask AcknowledgeAsync(ScanJobLease lease, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(lease.MessageId)) return;

        var db = _redis.GetDatabase();
        await db.StreamAcknowledgeAsync(_streamKey, _groupName, lease.MessageId);
        await db.StreamDeleteAsync(_streamKey, new RedisValue[] { lease.MessageId });

        _log.LogDebug("RedisScanQueue: XACK {MsgId}", lease.MessageId);
    }

    // ── Nack (re-enqueue for retry) ──────────────────────────────────────────

    public async ValueTask NackAsync(ScanJobLease lease, CancellationToken ct = default)
    {
        var db  = _redis.GetDatabase();
        var src = lease.Job;
        var retryJob = new ScanJob
        {
            DocumentId   = src.DocumentId,
            TenantId     = src.TenantId,
            VersionId    = src.VersionId,
            StorageKey   = src.StorageKey,
            FileName     = src.FileName,
            MimeType     = src.MimeType,
            EnqueuedAt   = DateTime.UtcNow,
            AttemptCount = src.AttemptCount + 1,
        };

        // ACK original to remove from PEL, then XADD new message for retry
        if (!string.IsNullOrEmpty(lease.MessageId))
        {
            await db.StreamAcknowledgeAsync(_streamKey, _groupName, lease.MessageId);
            await db.StreamDeleteAsync(_streamKey, new RedisValue[] { lease.MessageId });
        }

        var fields = SerializeJob(retryJob);
        await db.StreamAddAsync(_streamKey, fields,
            maxLength: _opts.StreamMaxLength > 0 ? _opts.StreamMaxLength : null,
            useApproximateMaxLength: true);

        _log.LogInformation("RedisScanQueue: re-enqueued retry attempt={Attempt} Document={DocId}",
            retryJob.AttemptCount, retryJob.DocumentId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task EnsureConsumerGroupAsync()
    {
        if (_groupEnsured) return;
        try
        {
            var db = _redis.GetDatabase();
            // MKSTREAM creates the stream if it doesn't exist
            await db.StreamCreateConsumerGroupAsync(_streamKey, _groupName, "0", createStream: true);
            _log.LogInformation("RedisScanQueue: consumer group '{Group}' ensured on '{Stream}'",
                _groupName, _streamKey);
        }
        catch (RedisException rex) when (rex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists — this is expected
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "RedisScanQueue: failed to ensure consumer group");
        }
        finally { _groupEnsured = true; }
    }

    private static NameValueEntry[] SerializeJob(ScanJob job) => new[]
    {
        new NameValueEntry("documentId",   job.DocumentId.ToString()),
        new NameValueEntry("tenantId",     job.TenantId.ToString()),
        new NameValueEntry("versionId",    job.VersionId?.ToString() ?? string.Empty),
        new NameValueEntry("storageKey",   job.StorageKey),
        new NameValueEntry("fileName",     job.FileName),
        new NameValueEntry("mimeType",     job.MimeType),
        new NameValueEntry("enqueuedAt",   job.EnqueuedAt.ToString("O")),
        new NameValueEntry("attemptCount", job.AttemptCount.ToString()),
    };

    private ScanJob? DeserializeJob(StreamEntry entry)
    {
        try
        {
            var fields = entry.Values.ToDictionary(e => (string)e.Name!, e => (string)e.Value!);

            return new ScanJob
            {
                DocumentId   = Guid.Parse(fields["documentId"]),
                TenantId     = Guid.Parse(fields["tenantId"]),
                VersionId    = string.IsNullOrEmpty(fields.GetValueOrDefault("versionId"))
                               ? null : Guid.Parse(fields["versionId"]),
                StorageKey   = fields["storageKey"],
                FileName     = fields["fileName"],
                MimeType     = fields["mimeType"],
                EnqueuedAt   = DateTime.Parse(fields.GetValueOrDefault("enqueuedAt") ?? DateTime.UtcNow.ToString("O")),
                AttemptCount = int.Parse(fields.GetValueOrDefault("attemptCount") ?? "0"),
            };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "RedisScanQueue: failed to deserialize stream entry {Id}", entry.Id);
            return null;
        }
    }

    public void Dispose() { /* IConnectionMultiplexer is shared singleton — not disposed here */ }
}
