namespace Documents.Infrastructure.Notifications;

/// <summary>
/// Top-level notifications configuration.
/// Bound from the "Notifications" section in appsettings.json.
/// </summary>
public sealed class NotificationOptions
{
    public ScanCompletionNotificationOptions ScanCompletion { get; set; } = new();
}

/// <summary>
/// Configuration for the DocumentScanCompleted event publisher.
/// </summary>
public sealed class ScanCompletionNotificationOptions
{
    /// <summary>
    /// Delivery provider:
    ///   "log"   — structured log only (default, safe for dev/test)
    ///   "redis" — publish to Redis Pub/Sub channel (production with Redis)
    ///   "none"  — no-op publisher (disable notifications entirely)
    /// </summary>
    public string Provider { get; set; } = "log";

    /// <summary>Redis Pub/Sub options (used when Provider=redis).</summary>
    public RedisNotificationOptions Redis { get; set; } = new();
}

/// <summary>
/// Redis Pub/Sub options for scan completion notifications.
/// </summary>
public sealed class RedisNotificationOptions
{
    /// <summary>Redis Pub/Sub channel name for scan completion events.</summary>
    public string Channel { get; set; } = "documents.scan.completed";
}
