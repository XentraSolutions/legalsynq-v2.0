using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.TaskService;

/// <summary>
/// TASK-MIG-07 — DISABLED.
///
/// This service previously synced all liens_TaskTemplates rows into the Task service
/// (Liens → Task direction) on every host startup. That direction is now WRONG:
/// after the MIG-07 ownership flip, the Task service is the primary write owner for
/// templates. Running this sync would overwrite Task-owned edits with stale Liens DB data.
///
/// The class is retained (not deleted) for two reasons:
///  1. Rollback safety — re-registering it as a HostedService in DependencyInjection.cs
///     immediately restores the pre-MIG-07 startup sync behavior with zero code change.
///  2. Reference — it documents the prior sync direction and the upsert payload shape.
///
/// Rollback instructions:
///  - In DependencyInjection.cs, uncomment the two LiensTemplateSyncService registrations.
///  - Revert LienTaskTemplateService.cs write methods to Liens-DB-primary.
///
/// Future cleanup (post MIG-08 or when liens_TaskTemplates is dropped):
///  - Delete this file and remove the registration comment from DependencyInjection.cs.
/// </summary>
public sealed class LiensTemplateSyncService : BackgroundService
{
    private readonly ILogger<LiensTemplateSyncService> _logger;

    public LiensTemplateSyncService(ILogger<LiensTemplateSyncService> logger)
    {
        _logger = logger;
    }

    protected override System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TASK-MIG-07: sync intentionally disabled.
        // Task service is now the primary write owner for templates.
        // Running Liens→Task sync would overwrite Task-owned data.
        _logger.LogInformation(
            "TASK-MIG-07: LiensTemplateSyncService is DISABLED (template_write_owner=task_service). "
            + "Liens→Task startup sync suppressed to protect Task-owned template data.");
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
