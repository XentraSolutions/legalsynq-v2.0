using Documents.Domain.Events;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace Documents.Infrastructure.Notifications;

/// <summary>
/// Redis Pub/Sub publisher — publishes <see cref="DocumentScanCompletedEvent"/> as a JSON
/// message to a configurable Redis channel after each terminal scan outcome.
///
/// Consumers can subscribe to this channel for real-time integration (e.g. CareConnect,
/// webhooks, or downstream workflow triggers).
///
/// Delivery guarantee: best-effort, at-most-once.
/// Redis Pub/Sub delivers to currently connected subscribers only.
/// For at-least-once semantics, use a Redis Stream (future extension point).
///
/// Failure handling: all exceptions are caught, logged, and measured. The scan pipeline
/// is never interrupted by a publish failure.
/// </summary>
public sealed class RedisScanCompletionPublisher : IScanCompletionPublisher
{
    private readonly IConnectionMultiplexer             _redis;
    private readonly ScanCompletionNotificationOptions  _opts;
    private readonly ILogger<RedisScanCompletionPublisher> _log;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false,
    };

    public RedisScanCompletionPublisher(
        IConnectionMultiplexer                       redis,
        IOptions<NotificationOptions>                opts,
        ILogger<RedisScanCompletionPublisher>        log)
    {
        _redis = redis;
        _opts  = opts.Value.ScanCompletion;
        _log   = log;
    }

    public async ValueTask PublishAsync(DocumentScanCompletedEvent evt, CancellationToken ct = default)
    {
        var statusLabel = evt.ScanStatus.ToString().ToLowerInvariant();
        try
        {
            var channel = _opts.Redis.Channel;
            var payload = JsonSerializer.Serialize(evt, JsonOpts);

            var sub         = _redis.GetSubscriber();
            var receiversHit = await sub.PublishAsync(
                RedisChannel.Literal(channel),
                payload,
                CommandFlags.FireAndForget);

            RedisMetrics.ScanCompletionEventsEmitted.WithLabels(statusLabel).Inc();
            RedisMetrics.ScanCompletionDeliverySuccess.Inc();

            _log.LogDebug(
                "DocumentScanCompleted published: Channel={Channel} EventId={EventId} " +
                "DocumentId={DocId} Status={Status} Receivers={Count}",
                channel, evt.EventId, evt.DocumentId, evt.ScanStatus, receiversHit);
        }
        catch (Exception ex)
        {
            RedisMetrics.ScanCompletionDeliveryFailures.Inc();
            RedisMetrics.RedisConnectionFailures.Inc();

            _log.LogWarning(ex,
                "RedisScanCompletionPublisher: publish failed for Document={DocId} Status={Status}. " +
                "Scan state is unaffected.",
                evt.DocumentId, evt.ScanStatus);

            // Increment emitted even on failure so operators can compute delivery rate
            RedisMetrics.ScanCompletionEventsEmitted.WithLabels(statusLabel).Inc();
        }
    }
}
