using System.ComponentModel.DataAnnotations;

namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Configuration options for the Xenia email ingestion engine.
/// Scoped at the application level; all limits are safety caps with conservative defaults.
/// </summary>
public sealed class XeniaIngestionOptions
{
    public const string SectionName = "XeniaIngestion";

    /// <summary>Master switch: enables all ingestion processing. Default: false (manual sync still works).</summary>
    public bool IngestionEnabled { get; set; } = true;

    /// <summary>Enables the background worker that periodically syncs all enabled sources. Default: false.</summary>
    public bool WorkerEnabled { get; set; } = false;

    // ── Page / message limits ─────────────────────────────────────────────────

    [Range(1, 500)]
    public int DefaultPageSize { get; set; } = 50;

    [Range(1, 500)]
    public int MaxPageSize { get; set; } = 100;

    [Range(1, 1000)]
    public int MaxPagesPerRun { get; set; } = 100;

    [Range(1, 50000)]
    public int MaxMessagesPerRun { get; set; } = 5000;

    // ── Size caps ─────────────────────────────────────────────────────────────

    /// <summary>Maximum body size in bytes (text or HTML). Default: 512 KB.</summary>
    [Range(1024, 10 * 1024 * 1024)]
    public int MaxMessageBodyBytes { get; set; } = 512 * 1024;

    /// <summary>Maximum total serialized header size in bytes. Default: 64 KB.</summary>
    [Range(256, 1024 * 1024)]
    public int MaxHeaderBytes { get; set; } = 64 * 1024;

    /// <summary>Maximum individual attachment size in bytes. Default: 20 MB.</summary>
    [Range(1024, 100 * 1024 * 1024)]
    public long MaxAttachmentBytes { get; set; } = 20L * 1024 * 1024;

    /// <summary>Maximum attachments per message to dispatch. Default: 20.</summary>
    [Range(1, 100)]
    public int MaxAttachmentsPerMessage { get; set; } = 20;

    // ── Timeouts ──────────────────────────────────────────────────────────────

    /// <summary>Total timeout for a single source sync run. Default: 5 minutes.</summary>
    public TimeSpan PerSourceTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Timeout for a single provider page/attachment request. Default: 30 seconds.</summary>
    public TimeSpan ProviderRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    // ── Retry ─────────────────────────────────────────────────────────────────

    [Range(0, 10)]
    public int MaxRetryCount { get; set; } = 3;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(5);

    // ── Worker ────────────────────────────────────────────────────────────────

    [Range(1, 20)]
    public int WorkerConcurrency { get; set; } = 2;

    public TimeSpan WorkerInterval { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan SourceLockLeaseDuration { get; set; } = TimeSpan.FromMinutes(10);

    // ── Body preview ──────────────────────────────────────────────────────────

    [Range(50, 2000)]
    public int BodyPreviewLength { get; set; } = 500;

    // ── HTML ──────────────────────────────────────────────────────────────────

    /// <summary>Block remote images by default to prevent tracking. Default: true.</summary>
    public bool BlockRemoteImages { get; set; } = true;

    /// <summary>Strip all script tags and event handlers from HTML bodies. Default: true.</summary>
    public bool StripHtmlScripts { get; set; } = true;
}
