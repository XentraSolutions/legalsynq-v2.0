using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.TaskService;

/// <summary>
/// TASK-MIG-08 — DISABLED.
///
/// This service previously synced all liens_TaskGovernanceSettings rows into the Task service
/// (Liens → Task direction) on every host startup. That direction is now WRONG:
/// after the MIG-08 ownership flip, the Task service is the primary write owner for
/// governance settings. Running this sync would overwrite Task-owned edits with stale Liens DB data.
///
/// The class is retained (not deleted) for rollback safety:
///  - Re-registering it as a HostedService in DependencyInjection.cs restores the prior behavior.
///  - The sync body (MigrateAllAsync logic) is preserved in git history for reference.
///
/// Rollback instructions:
///  - In LiensGovernanceSyncService.cs, restore the ExecuteAsync scan-and-upsert loop.
///  - In LienTaskGovernanceService.cs, revert UpdateAsync and GetOrCreateAsync to Liens-DB-primary.
///
/// Future cleanup (post MIG-09 or when liens_TaskGovernanceSettings is ready to drop):
///  - Delete this file and remove the registration comment from DependencyInjection.cs.
/// </summary>
public sealed class LiensGovernanceSyncService : BackgroundService
{
    private readonly ILogger<LiensGovernanceSyncService> _logger;

    public LiensGovernanceSyncService(ILogger<LiensGovernanceSyncService> logger)
    {
        _logger = logger;
    }

    protected override System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TASK-MIG-08: sync intentionally disabled.
        // Task service is now the primary write owner for governance settings.
        // Running Liens→Task sync would overwrite Task-owned data.
        _logger.LogInformation(
            "TASK-MIG-08: LiensGovernanceSyncService is DISABLED (governance_write_owner=task_service). "
            + "Liens→Task startup sync suppressed to protect Task-owned governance data.");
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
