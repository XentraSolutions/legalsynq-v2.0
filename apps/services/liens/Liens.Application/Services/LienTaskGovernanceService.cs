using System.Text.Json;
using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// LS-LIENS-FLOW-006 / TASK-MIG-01 — Manages task governance settings for a tenant.
///
/// Read strategy (dual-read):
///   1. Try Task service (GET /api/tasks/governance?sourceProductCode=SYNQ_LIENS).
///   2. Fall back to Liens DB if Task service returns no settings or an error.
///
/// Write strategy (write-through):
///   1. Always write to Liens DB (source of truth during migration).
///   2. Best-effort sync to Task service; failure is logged but does NOT abort the request.
///
/// Migration safety:
///   - Liens DB is NEVER deleted in this step.
///   - Fallback ensures zero behavior regression.
/// </summary>
public sealed class LienTaskGovernanceService : ILienTaskGovernanceService
{
    private const string ProductCode = LiensPermissions.ProductCode; // "SYNQ_LIENS"

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly ILienTaskGovernanceSettingsRepository _repo;
    private readonly ILiensTaskServiceClient               _taskClient;
    private readonly IAuditPublisher                       _audit;
    private readonly ILogger<LienTaskGovernanceService>    _logger;

    public LienTaskGovernanceService(
        ILienTaskGovernanceSettingsRepository repo,
        ILiensTaskServiceClient               taskClient,
        IAuditPublisher                       audit,
        ILogger<LienTaskGovernanceService>    logger)
    {
        _repo       = repo;
        _taskClient = taskClient;
        _audit      = audit;
        _logger     = logger;
    }

    // ── GetAsync — dual-read, no create ─────────────────────────────────────────

    public async Task<TaskGovernanceSettingsResponse?> GetAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        // 1. Try Task service (primary)
        var fromTask = await TryFetchFromTaskServiceAsync(tenantId, ct);
        if (fromTask is not null)
            return fromTask;

        // 2. Fall back to Liens DB
        var entity = await _repo.GetByTenantProductAsync(tenantId, ProductCode, ct);
        if (entity is not null)
        {
            _logger.LogDebug(
                "governance_source=liens_fallback TenantId={TenantId}", tenantId);
            return MapToResponse(entity);
        }

        return null;
    }

    // ── GetOrCreateAsync — dual-read with Liens-side default creation ────────────

    public async Task<TaskGovernanceSettingsResponse> GetOrCreateAsync(
        Guid tenantId, Guid actingUserId, string updateSource, CancellationToken ct = default)
    {
        // 1. Try Task service (primary)
        var fromTask = await TryFetchFromTaskServiceAsync(tenantId, ct);
        if (fromTask is not null)
            return fromTask;

        // 2. Try Liens DB
        var entity = await _repo.GetByTenantProductAsync(tenantId, ProductCode, ct);
        if (entity is not null)
        {
            _logger.LogDebug(
                "governance_source=liens_fallback TenantId={TenantId}", tenantId);
            return MapToResponse(entity);
        }

        // 3. Create default in Liens DB (source of truth)
        var newEntity = LienTaskGovernanceSettings.CreateDefault(
            tenantId:        tenantId,
            productCode:     ProductCode,
            updateSource:    updateSource,
            createdByUserId: actingUserId);

        await _repo.AddAsync(newEntity, ct);

        _audit.Publish(
            eventType:   "liens.task_governance.created",
            action:      "create",
            description: "Task governance settings created with defaults",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskGovernanceSettings",
            entityId:    newEntity.Id.ToString());

        _logger.LogInformation(
            "governance_source=liens_created TenantId={TenantId}", tenantId);

        // 4. Best-effort sync new defaults to Task service
        await TrySyncToTaskServiceAsync(tenantId, actingUserId, newEntity, expectedVersion: 0, ct);

        return MapToResponse(newEntity);
    }

    // ── UpdateAsync — write-through ──────────────────────────────────────────────

    public async Task<TaskGovernanceSettingsResponse> UpdateAsync(
        Guid tenantId, Guid actingUserId,
        UpdateTaskGovernanceSettingsRequest request, CancellationToken ct = default)
    {
        var entity = await _repo.GetByTenantProductAsync(tenantId, ProductCode, ct);

        if (entity is null)
        {
            entity = LienTaskGovernanceSettings.CreateDefault(
                tenantId:        tenantId,
                productCode:     ProductCode,
                updateSource:    request.UpdateSource,
                createdByUserId: actingUserId);
            await _repo.AddAsync(entity, ct);
        }
        else if (entity.Version != request.Version)
        {
            throw new ConflictException(
                $"Governance settings were modified by another user (expected version {request.Version}, current {entity.Version}). Please reload and try again.");
        }

        entity.Update(
            requireAssigneeOnCreate:      request.RequireAssigneeOnCreate,
            requireCaseLinkOnCreate:      request.RequireCaseLinkOnCreate,
            allowMultipleAssignees:       request.AllowMultipleAssignees,
            requireWorkflowStageOnCreate: request.RequireWorkflowStageOnCreate,
            defaultStartStageMode:        request.DefaultStartStageMode,
            explicitStartStageId:         request.ExplicitStartStageId,
            updateSource:                 request.UpdateSource,
            updatedByUserId:              actingUserId,
            updatedByName:                request.UpdatedByName);

        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.task_governance.updated",
            action:      "update",
            description: "Task governance settings updated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskGovernanceSettings",
            entityId:    entity.Id.ToString());

        _logger.LogInformation(
            "Task governance settings updated for tenant {TenantId} by user {UserId}.", tenantId, actingUserId);

        // Best-effort write-through to Task service
        await TrySyncToTaskServiceAsync(tenantId, actingUserId, entity, expectedVersion: 0, ct);

        return MapToResponse(entity);
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Calls the Task service governance endpoint and maps the response to the
    /// Liens DTO. Returns null if no settings are found or the call fails.
    /// Never throws — failures are logged and callers fall back to Liens DB.
    /// </summary>
    private async Task<TaskGovernanceSettingsResponse?> TryFetchFromTaskServiceAsync(
        Guid tenantId, CancellationToken ct)
    {
        try
        {
            var dto = await _taskClient.GetGovernanceAsync(tenantId, ProductCode, ct);
            if (dto is null)
                return null;

            var extensions = DeserializeExtensions(dto.ProductSettingsJson);

            _logger.LogDebug(
                "governance_source=task_service TenantId={TenantId}", tenantId);

            return new TaskGovernanceSettingsResponse
            {
                Id                       = dto.Id,
                TenantId                 = dto.TenantId,
                ProductCode              = ProductCode,
                RequireAssigneeOnCreate  = dto.RequireAssignee,
                RequireCaseLinkOnCreate  = extensions.RequireCaseLinkOnCreate,
                AllowMultipleAssignees   = extensions.AllowMultipleAssignees,
                RequireWorkflowStageOnCreate = dto.RequireStage,
                DefaultStartStageMode    = extensions.DefaultStartStageMode,
                ExplicitStartStageId     = extensions.ExplicitStartStageId,
                Version                  = dto.Version,
                LastUpdatedAt            = dto.UpdatedAtUtc,
                LastUpdatedByUserId      = null,
                LastUpdatedByName        = null,
                LastUpdatedSource        = WorkflowUpdateSources.TaskServiceSync,
                CreatedAtUtc             = dto.CreatedAtUtc,
                UpdatedAtUtc             = dto.UpdatedAtUtc,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "governance_source=task_service_error TenantId={TenantId}; falling back to Liens DB.",
                tenantId);
            return null;
        }
    }

    /// <summary>
    /// Serializes a <see cref="LienTaskGovernanceSettings"/> entity into a
    /// <see cref="TaskServiceGovernanceUpsertRequest"/> and sends it to the Task service.
    /// Failures are logged but not re-thrown — the Liens DB remains authoritative.
    /// </summary>
    private async System.Threading.Tasks.Task TrySyncToTaskServiceAsync(
        Guid                      tenantId,
        Guid                      actingUserId,
        LienTaskGovernanceSettings entity,
        int                       expectedVersion,
        CancellationToken         ct)
    {
        try
        {
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
                SourceProductCode         = ProductCode,
                ExpectedVersion           = expectedVersion,
                ProductSettingsJson       = JsonSerializer.Serialize(extensions, _json),
            };

            await _taskClient.UpsertGovernanceAsync(tenantId, actingUserId, payload, ct);

            _logger.LogInformation(
                "governance_sync=ok TenantId={TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "governance_sync=failed TenantId={TenantId}; Liens DB remains authoritative.",
                tenantId);
        }
    }

    private static LiensGovernanceExtensions DeserializeExtensions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new LiensGovernanceExtensions();

        try
        {
            return JsonSerializer.Deserialize<LiensGovernanceExtensions>(json, _json)
                   ?? new LiensGovernanceExtensions();
        }
        catch
        {
            return new LiensGovernanceExtensions();
        }
    }

    private static TaskGovernanceSettingsResponse MapToResponse(LienTaskGovernanceSettings e) =>
        new()
        {
            Id                       = e.Id,
            TenantId                 = e.TenantId,
            ProductCode              = e.ProductCode,
            RequireAssigneeOnCreate  = e.RequireAssigneeOnCreate,
            RequireCaseLinkOnCreate  = e.RequireCaseLinkOnCreate,
            AllowMultipleAssignees   = e.AllowMultipleAssignees,
            RequireWorkflowStageOnCreate = e.RequireWorkflowStageOnCreate,
            DefaultStartStageMode    = e.DefaultStartStageMode,
            ExplicitStartStageId     = e.ExplicitStartStageId,
            Version                  = e.Version,
            LastUpdatedAt            = e.LastUpdatedAt,
            LastUpdatedByUserId      = e.LastUpdatedByUserId,
            LastUpdatedByName        = e.LastUpdatedByName,
            LastUpdatedSource        = e.LastUpdatedSource,
            CreatedAtUtc             = e.CreatedAtUtc,
            UpdatedAtUtc             = e.UpdatedAtUtc,
        };
}
