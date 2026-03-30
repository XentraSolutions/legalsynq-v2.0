using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs.Retention;
using PlatformAuditEventService.Services;

namespace PlatformAuditEventService.Jobs;

/// <summary>
/// Scheduled retention policy evaluation and (future) enforcement job.
///
/// v1 behaviour — evaluation only, no side effects:
///   1. Checks whether the job is enabled (<c>Retention:JobEnabled</c>).
///   2. Calls <see cref="IRetentionService.EvaluateAsync"/> to classify a sample
///      of the oldest records into storage tiers (Hot / Warm / Cold / Indefinite).
///   3. Logs a structured summary of the evaluation result.
///   4. Issues a Warning log when Cold-tier (expired) records are found, pointing
///      operators to the configuration needed to activate archival.
///   5. Returns without modifying, archiving, or deleting any records.
///
/// Future activation path:
///   1. Set <c>Retention:ArchiveBeforeDelete=true</c> and configure
///      <c>Archival:Strategy</c> to a real provider (S3, AzureBlob, LocalCopy).
///   2. Set <c>Retention:DryRun=false</c> after validating the archival pipeline.
///   3. Register this job as a <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>
///      (simple) or as a Quartz.NET trigger (production scheduling with cron support).
///
/// Scheduling:
///   The cron expression in <c>Retention:JobCronUtc</c> (default: "0 2 * * *" = 02:00 UTC daily)
///   is informational in v1 — this job does not wire its own scheduler. Future integration
///   with Quartz.NET should register this job using that expression.
/// </summary>
public sealed class RetentionPolicyJob
{
    private readonly IRetentionService             _retentionService;
    private readonly RetentionOptions              _opts;
    private readonly ILogger<RetentionPolicyJob>   _logger;

    public RetentionPolicyJob(
        IRetentionService             retentionService,
        IOptions<RetentionOptions>    opts,
        ILogger<RetentionPolicyJob>   logger)
    {
        _retentionService = retentionService;
        _opts             = opts.Value;
        _logger           = logger;
    }

    /// <summary>
    /// Execute one retention policy run.
    ///
    /// In v1, this is always a dry-run evaluation. Archival and deletion
    /// are not performed regardless of configuration.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (!_opts.JobEnabled)
        {
            _logger.LogDebug(
                "RetentionPolicyJob: skipped — job is disabled (Retention:JobEnabled=false).");
            return;
        }

        _logger.LogInformation(
            "RetentionPolicyJob: starting evaluation. " +
            "DryRun={DryRun} ArchiveBeforeDelete={Archive} HotDays={Hot} DefaultDays={Default}",
            _opts.DryRun, _opts.ArchiveBeforeDelete, _opts.HotRetentionDays,
            _opts.DefaultRetentionDays <= 0 ? "indefinite" : _opts.DefaultRetentionDays.ToString());

        // ── Phase 1: Policy evaluation (always runs, always read-only) ────────
        var result = await _retentionService.EvaluateAsync(
            new RetentionEvaluationRequest
            {
                SampleLimit = _opts.MaxDeletesPerRun > 0 ? _opts.MaxDeletesPerRun : 5_000,
            },
            ct);

        _logger.LogInformation(
            "RetentionPolicyJob evaluation: " +
            "TotalInStore={Total} Sampled={Sampled} | " +
            "Hot={Hot} Warm={Warm} Cold={Cold} Indefinite={Indefinite}",
            result.TotalRecordsInStore, result.SampleRecordsClassified,
            result.RecordsInHotTier, result.RecordsInWarmTier,
            result.RecordsInColdTier, result.RecordsIndefinite);

        _logger.LogInformation(
            "RetentionPolicyJob policy: {PolicySummary}", result.PolicySummary);

        // ── Warning: expired records found ────────────────────────────────────
        if (result.RecordsExpiredInSample > 0)
        {
            _logger.LogWarning(
                "RetentionPolicyJob: {ExpiredCount} records in sample are in the Cold tier " +
                "(past their retention window). No action taken. " +
                "To activate archival: set Retention:ArchiveBeforeDelete=true, configure " +
                "Archival:Strategy, and set Retention:DryRun=false.",
                result.RecordsExpiredInSample);

            foreach (var (category, count) in result.ExpiredByCategory)
            {
                _logger.LogWarning(
                    "RetentionPolicyJob: expired by category — {Category}: {Count}",
                    category, count);
            }
        }

        // ── Phase 2: Archival (future — not implemented in v1) ────────────────
        if (!_opts.DryRun && _opts.ArchiveBeforeDelete)
        {
            _logger.LogWarning(
                "RetentionPolicyJob: DryRun=false and ArchiveBeforeDelete=true are set, " +
                "but archival execution is not implemented in v1. " +
                "Cold-tier records were NOT archived or deleted this run.");
        }

        _logger.LogInformation("RetentionPolicyJob: run complete.");
    }
}
