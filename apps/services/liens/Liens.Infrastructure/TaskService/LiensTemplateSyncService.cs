using Liens.Application.Repositories;
using Liens.Application.Services;
using Liens.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.TaskService;

/// <summary>
/// TASK-MIG-02 — Startup sync service that copies all liens_TaskTemplates rows
/// into tasks_Templates (Task service) on every host startup.
///
/// Rules:
///   - Runs once, 5 seconds after startup, then exits.
///   - Idempotent — uses the Task service upsert-from-source endpoint.
///   - Per-template errors are logged and skipped; the loop continues.
///   - Does NOT modify or delete liens_TaskTemplates.
///   - Uses system migration user 00000000-0000-0000-0000-000000000001.
/// </summary>
public sealed class LiensTemplateSyncService : BackgroundService
{
    private static readonly Guid SystemUserId =
        new("00000000-0000-0000-0000-000000000001");

    private readonly IServiceScopeFactory                   _scopeFactory;
    private readonly ILogger<LiensTemplateSyncService>      _logger;

    public LiensTemplateSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<LiensTemplateSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        _logger.LogInformation("TASK-MIG-02: template sync starting.");

        using var scope = _scopeFactory.CreateScope();
        var repo        = scope.ServiceProvider.GetRequiredService<ILienTaskTemplateRepository>();
        var taskClient  = scope.ServiceProvider.GetRequiredService<ILiensTaskServiceClient>();

        List<Liens.Domain.Entities.LienTaskTemplate> templates;
        try
        {
            templates = await repo.GetAllAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TASK-MIG-02: failed to load templates from Liens DB; aborting sync.");
            return;
        }

        int created = 0, updated = 0, errors = 0;

        foreach (var entity in templates)
        {
            try
            {
                var existing = await taskClient.GetTemplateAsync(entity.TenantId, entity.Id, stoppingToken);
                var payload  = LienTaskTemplateService.MapToUpsertPayload(entity);

                await taskClient.UpsertTemplateFromSourceAsync(entity.TenantId, SystemUserId, payload, stoppingToken);

                if (existing is null) created++;
                else                  updated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "TASK-MIG-02: failed to sync TemplateId={TemplateId} TenantId={TenantId}; skipping.",
                    entity.Id, entity.TenantId);
                errors++;
            }
        }

        _logger.LogInformation(
            "TASK-MIG-02: sync complete — created={Created} updated={Updated} errors={Errors}",
            created, updated, errors);
    }
}
