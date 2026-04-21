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
/// LS-LIENS-FLOW-006 / TASK-MIG-08 — Governance Ownership Flip (Liens → Task).
///
/// Write authority : Task service (primary). All admin writes (UpdateAsync, GetOrCreateAsync
///                  default-create) go to Task service first.
///                  Liens DB receives a best-effort mirror write for rollback safety only.
///                  A failed Task service write is fatal (not swallowed); a failed Liens
///                  mirror write is logged and tolerated.
///
/// Read authority  : Task service (primary) for all reads.
///                  Liens DB is retained as fallback only.
///                  governance_write_owner=task_service / governance_read_owner=task_service
///
/// Startup sync    : LiensGovernanceSyncService (Liens→Task direction) is DISABLED post-MIG-08.
///                  It would overwrite Task-owned data with stale Liens DB copies.
///
/// Rollback        : Re-enable Liens DB as primary in UpdateAsync + GetOrCreateAsync, and
///                  re-enable LiensGovernanceSyncService body in DependencyInjection.cs.
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

    // ── GetAsync — Task-first read (unchanged from MIG-01) ───────────────────────

    public async Task<TaskGovernanceSettingsResponse?> GetAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var fromTask = await TryFetchFromTaskServiceAsync(tenantId, ct);
        if (fromTask is not null)
            return fromTask;

        var entity = await _repo.GetByTenantProductAsync(tenantId, ProductCode, ct);
        if (entity is not null)
        {
            _logger.LogDebug(
                "governance_read_owner=liens_db_fallback GetAsync TenantId={TenantId}", tenantId);
            return MapToResponse(entity);
        }

        return null;
    }

    // ── GetOrCreateAsync — TASK-MIG-08: create in Task service first ─────────────

    public async Task<TaskGovernanceSettingsResponse> GetOrCreateAsync(
        Guid tenantId, Guid actingUserId, string updateSource, CancellationToken ct = default)
    {
        // 1. Task service (primary read)
        var fromTask = await TryFetchFromTaskServiceAsync(tenantId, ct);
        if (fromTask is not null)
            return fromTask;

        // 2. Liens DB fallback
        var entity = await _repo.GetByTenantProductAsync(tenantId, ProductCode, ct);
        if (entity is not null)
        {
            _logger.LogDebug(
                "governance_read_owner=liens_db_fallback GetOrCreateAsync TenantId={TenantId}", tenantId);
            return MapToResponse(entity);
        }

        // 3. Neither exists — TASK-MIG-08: create in Task service FIRST (primary owner).
        var newEntity = LienTaskGovernanceSettings.CreateDefault(
            tenantId:        tenantId,
            productCode:     ProductCode,
            updateSource:    updateSource,
            createdByUserId: actingUserId);

        // PRIMARY WRITE — Task service
        var payload = BuildUpsertPayload(newEntity);
        await _taskClient.UpsertGovernanceAsync(tenantId, actingUserId, payload, ct);

        _logger.LogInformation(
            "governance_write_owner=task_service Created defaults TenantId={TenantId}", tenantId);

        _audit.Publish(
            eventType:   "liens.task_governance.created",
            action:      "create",
            description: "Task governance settings created with defaults (Task service primary)",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskGovernanceSettings",
            entityId:    newEntity.Id.ToString());

        // MIRROR — Liens DB best-effort rollback safety
        await TryMirrorCreateToLiensDbAsync(tenantId, newEntity, ct);

        return MapToResponse(newEntity);
    }

    // ── UpdateAsync — TASK-MIG-08: Task service primary write ────────────────────

    public async Task<TaskGovernanceSettingsResponse> UpdateAsync(
        Guid tenantId, Guid actingUserId,
        UpdateTaskGovernanceSettingsRequest request, CancellationToken ct = default)
    {
        // Load from Liens DB for version conflict detection (transitional version authority).
        var entity = await _repo.GetByTenantProductAsync(tenantId, ProductCode, ct);

        if (entity is null)
        {
            // First-time upsert: create default in-memory, apply request fields.
            entity = LienTaskGovernanceSettings.CreateDefault(
                tenantId:        tenantId,
                productCode:     ProductCode,
                updateSource:    request.UpdateSource,
                createdByUserId: actingUserId);
        }
        else if (entity.Version != request.Version)
        {
            throw new ConflictException(
                $"Governance settings were modified by another user (expected version {request.Version}, current {entity.Version}). Please reload and try again.");
        }

        // Apply changes in-memory (Liens DB not written yet).
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

        // PRIMARY WRITE — Task service (throws on failure)
        var payload = BuildUpsertPayload(entity);
        await _taskClient.UpsertGovernanceAsync(tenantId, actingUserId, payload, ct);

        _logger.LogInformation(
            "governance_write_owner=task_service Updated TenantId={TenantId} by UserId={UserId}",
            tenantId, actingUserId);

        _audit.Publish(
            eventType:   "liens.task_governance.updated",
            action:      "update",
            description: "Task governance settings updated (Task service primary)",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskGovernanceSettings",
            entityId:    entity.Id.ToString());

        // MIRROR — Liens DB best-effort rollback safety
        await TryMirrorUpdateToLiensDbAsync(tenantId, entity, ct);

        return MapToResponse(entity);
    }

    // ── Read helper ──────────────────────────────────────────────────────────────

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
                "governance_read_owner=task_service TenantId={TenantId}", tenantId);

            return new TaskGovernanceSettingsResponse
            {
                Id                           = dto.Id,
                TenantId                     = dto.TenantId,
                ProductCode                  = ProductCode,
                RequireAssigneeOnCreate      = dto.RequireAssignee,
                RequireCaseLinkOnCreate      = extensions.RequireCaseLinkOnCreate,
                AllowMultipleAssignees       = extensions.AllowMultipleAssignees,
                RequireWorkflowStageOnCreate = dto.RequireStage,
                DefaultStartStageMode        = extensions.DefaultStartStageMode,
                ExplicitStartStageId         = extensions.ExplicitStartStageId,
                Version                      = dto.Version,
                LastUpdatedAt                = dto.UpdatedAtUtc,
                LastUpdatedByUserId          = null,
                LastUpdatedByName            = null,
                LastUpdatedSource            = WorkflowUpdateSources.TaskServiceSync,
                CreatedAtUtc                 = dto.CreatedAtUtc,
                UpdatedAtUtc                 = dto.UpdatedAtUtc,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "governance_read_owner=task_service_error TenantId={TenantId}; falling back to Liens DB.",
                tenantId);
            return null;
        }
    }

    // ── Mirror helpers (Liens DB rollback safety — non-authoritative) ─────────────

    private async System.Threading.Tasks.Task TryMirrorCreateToLiensDbAsync(
        Guid tenantId, LienTaskGovernanceSettings entity, CancellationToken ct)
    {
        try
        {
            await _repo.AddAsync(entity, ct);
            _logger.LogDebug(
                "governance_mirror_target=liens_db Create TenantId={TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "governance_mirror_target=liens_db_failed Create TenantId={TenantId}; "
                + "Task service write succeeded — Liens DB mirror is non-authoritative.",
                tenantId);
        }
    }

    private async System.Threading.Tasks.Task TryMirrorUpdateToLiensDbAsync(
        Guid tenantId, LienTaskGovernanceSettings entity, CancellationToken ct)
    {
        try
        {
            // Upsert pattern: if the entity doesn't exist in Liens DB yet (mirror drifted),
            // check and add/update accordingly.
            var existing = await _repo.GetByTenantProductAsync(tenantId, ProductCode, ct);
            if (existing is null)
                await _repo.AddAsync(entity, ct);
            else
                await _repo.UpdateAsync(entity, ct);

            _logger.LogDebug(
                "governance_mirror_target=liens_db Update TenantId={TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "governance_mirror_target=liens_db_failed Update TenantId={TenantId}; "
                + "Task service write succeeded — Liens DB mirror is non-authoritative.",
                tenantId);
        }
    }

    // ── Payload builder ──────────────────────────────────────────────────────────

    private static TaskServiceGovernanceUpsertRequest BuildUpsertPayload(
        LienTaskGovernanceSettings entity)
    {
        var extensions = new LiensGovernanceExtensions
        {
            RequireCaseLinkOnCreate = entity.RequireCaseLinkOnCreate,
            AllowMultipleAssignees  = entity.AllowMultipleAssignees,
            DefaultStartStageMode   = entity.DefaultStartStageMode,
            ExplicitStartStageId    = entity.ExplicitStartStageId,
        };

        return new TaskServiceGovernanceUpsertRequest
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
            ExpectedVersion           = 0,
            ProductSettingsJson       = JsonSerializer.Serialize(extensions, _json),
        };
    }

    // ── Mapping helpers ──────────────────────────────────────────────────────────

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
            Id                           = e.Id,
            TenantId                     = e.TenantId,
            ProductCode                  = e.ProductCode,
            RequireAssigneeOnCreate      = e.RequireAssigneeOnCreate,
            RequireCaseLinkOnCreate      = e.RequireCaseLinkOnCreate,
            AllowMultipleAssignees       = e.AllowMultipleAssignees,
            RequireWorkflowStageOnCreate = e.RequireWorkflowStageOnCreate,
            DefaultStartStageMode        = e.DefaultStartStageMode,
            ExplicitStartStageId         = e.ExplicitStartStageId,
            Version                      = e.Version,
            LastUpdatedAt                = e.LastUpdatedAt,
            LastUpdatedByUserId          = e.LastUpdatedByUserId,
            LastUpdatedByName            = e.LastUpdatedByName,
            LastUpdatedSource            = e.LastUpdatedSource,
            CreatedAtUtc                 = e.CreatedAtUtc,
            UpdatedAtUtc                 = e.UpdatedAtUtc,
        };
}
