namespace PlatformAuditEventService.Jobs;

/// <summary>
/// Placeholder for a scheduled retention policy background job.
/// Future implementation should archive or purge audit events
/// according to configured per-tenant or platform-level retention rules.
///
/// Recommended implementation: IHostedService or Quartz.NET / Hangfire scheduler.
/// Retention policy options should be driven by AuditServiceOptions (e.g. RetentionDays).
///
/// IMPORTANT: Audit records are immutable during their retention period.
/// Deletion must require an explicit compliance workflow, not a silent background purge.
/// </summary>
public sealed class RetentionPolicyJob
{
    private readonly ILogger<RetentionPolicyJob> _logger;

    public RetentionPolicyJob(ILogger<RetentionPolicyJob> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("RetentionPolicyJob: placeholder — no retention rules configured.");
        return Task.CompletedTask;
    }
}
