using System.Text.Json;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.TaskService;

/// <summary>
/// TASK-MIG-01 — Startup background service that migrates all existing
/// <c>liens_TaskGovernanceSettings</c> rows into <c>tasks_GovernanceSettings</c>.
///
/// Behaviour:
/// - Runs once on startup (after the host is running).
/// - Upserts one Task-service row per Liens governance row.
/// - Idempotent: safe to run multiple times.
/// - Failures per-tenant are logged and skipped; they do NOT abort the run.
/// - Never deletes or modifies Liens data.
/// </summary>
public sealed class LiensGovernanceSyncService : BackgroundService
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory                  _scopeFactory;
    private readonly ILogger<LiensGovernanceSyncService>   _logger;

    public LiensGovernanceSyncService(
        IServiceScopeFactory                _scopeFactory,
        ILogger<LiensGovernanceSyncService> logger)
    {
        this._scopeFactory = _scopeFactory;
        _logger            = logger;
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay so the host is fully started before migration runs
        await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        _logger.LogInformation("TASK-MIG-01: governance sync starting.");

        try
        {
            await MigrateAllAsync(stoppingToken);
            _logger.LogInformation("TASK-MIG-01: governance sync complete.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TASK-MIG-01: governance sync failed unexpectedly; Liens DB remains authoritative.");
        }
    }

    private async System.Threading.Tasks.Task MigrateAllAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var governanceRepo = scope.ServiceProvider
            .GetRequiredService<ILienTaskGovernanceSettingsRepository>();
        var taskClient = scope.ServiceProvider
            .GetRequiredService<ILiensTaskServiceClient>();

        var allRows = await governanceRepo.GetAllAsync(ct);

        int created = 0, updated = 0, skipped = 0, errors = 0;

        foreach (var entity in allRows)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Check if already exists in Task service
                var existing = await taskClient.GetGovernanceAsync(
                    entity.TenantId, LiensPermissions.ProductCode, ct);

                var extensions = new LiensGovernanceExtensions
                {
                    RequireCaseLinkOnCreate = entity.RequireCaseLinkOnCreate,
                    AllowMultipleAssignees  = entity.AllowMultipleAssignees,
                    DefaultStartStageMode   = entity.DefaultStartStageMode,
                    ExplicitStartStageId    = entity.ExplicitStartStageId,
                };

                var payload = new TaskServiceGovernanceUpsertRequest
                {
                    RequireAssignee           = entity.RequireAssigneeOnCreate,
                    RequireDueDate            = false,
                    RequireStage              = entity.RequireWorkflowStageOnCreate,
                    AllowUnassign             = true,
                    AllowCancel               = true,
                    AllowCompleteWithoutStage = !entity.RequireWorkflowStageOnCreate,
                    AllowNotesOnClosedTasks   = false,
                    DefaultPriority           = "MEDIUM",
                    DefaultTaskScope          = "GENERAL",
                    SourceProductCode         = LiensPermissions.ProductCode,
                    ExpectedVersion           = 0,
                    ProductSettingsJson       = JsonSerializer.Serialize(extensions, _json),
                };

                // System user ID used for the upsert (static migration actor)
                var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                await taskClient.UpsertGovernanceAsync(entity.TenantId, systemUserId, payload, ct);

                if (existing is null)
                {
                    created++;
                    _logger.LogInformation(
                        "TASK-MIG-01: created governance in Task service for TenantId={TenantId}", entity.TenantId);
                }
                else
                {
                    updated++;
                    _logger.LogInformation(
                        "TASK-MIG-01: updated governance in Task service for TenantId={TenantId}", entity.TenantId);
                }
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogWarning(ex,
                    "TASK-MIG-01: failed to sync TenantId={TenantId}; skipping.", entity.TenantId);
            }
        }

        _logger.LogInformation(
            "TASK-MIG-01: sync complete — created={Created} updated={Updated} skipped={Skipped} errors={Errors}",
            created, updated, skipped, errors);
    }
}
