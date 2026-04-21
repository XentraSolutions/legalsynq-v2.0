using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// TASK-MIG-07 — Template Ownership Flip (Liens → Task).
///
/// Write authority : Task service (primary). All admin writes go to Task service first.
///                  Liens DB receives a best-effort mirror write for rollback safety only.
///                  A failed Task service write is fatal (not swallowed); a failed Liens
///                  mirror write is logged and tolerated.
///
/// Read authority  : Task service (primary) for admin list/get-by-id, generation, and
///                  contextual reads. Liens DB fallback retained for resilience only.
///                  template_write_owner=task_service / template_read_owner=task_service
///
/// Startup sync    : LiensTemplateSyncService (Liens→Task direction) is DISABLED post-MIG-07.
///                  It would overwrite Task-owned data with stale Liens DB copies.
///
/// Rollback        : Re-enable Liens DB as primary write target in CreateAsync/UpdateAsync/
///                  ActivateAsync/DeactivateAsync, and re-enable LiensTemplateSyncService
///                  registration in DependencyInjection.cs.
/// </summary>
public sealed class LienTaskTemplateService : ILienTaskTemplateService
{
    private const string ProductCode   = "SYNQ_LIENS";
    private const string DefaultScope  = "GENERAL";

    private readonly ILienTaskTemplateRepository      _repo;
    private readonly ILiensTaskServiceClient          _taskClient;
    private readonly IAuditPublisher                  _audit;
    private readonly ILogger<LienTaskTemplateService> _logger;

    public LienTaskTemplateService(
        ILienTaskTemplateRepository repo,
        ILiensTaskServiceClient taskClient,
        IAuditPublisher audit,
        ILogger<LienTaskTemplateService> logger)
    {
        _repo       = repo;
        _taskClient = taskClient;
        _audit      = audit;
        _logger     = logger;
    }

    // ── TASK-MIG-07 — Admin reads: Task service primary, Liens DB fallback ────────

    public async Task<List<TaskTemplateResponse>> GetByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            var all = await _taskClient.GetAllTemplatesAsync(tenantId, ProductCode, ct);
            if (all.Count > 0)
            {
                _logger.LogDebug(
                    "template_read_owner=task_service GetByTenant TenantId={TenantId} Count={Count}",
                    tenantId, all.Count);
                return all.Select(MapFromTaskServiceDto).ToList();
            }

            _logger.LogDebug(
                "template_read_owner=liens_db_fallback GetByTenant TenantId={TenantId} (Task service returned 0 templates)",
                tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "template_read_owner=liens_db_fallback GetByTenant TenantId={TenantId} (Task service error)",
                tenantId);
        }

        var list = await _repo.GetByTenantAsync(tenantId, ct);
        return list.Select(MapToResponse).ToList();
    }

    public async Task<List<TaskTemplateResponse>> GetContextualAsync(
        Guid tenantId, string? contextType, Guid? workflowStageId, CancellationToken ct = default)
    {
        // TASK-MIG-05 — dual-read for contextual UI filter (unchanged).
        // Task service is primary; Liens DB is fallback.
        var fromTask = await TryGetContextualFromTaskServiceAsync(tenantId, contextType, workflowStageId, ct);
        if (fromTask is not null)
        {
            _logger.LogDebug(
                "template_contextual_source=task_service_filtered TenantId={TenantId} contextType={ContextType} stageId={StageId} Count={Count}",
                tenantId, contextType, workflowStageId, fromTask.Count);
            return fromTask;
        }

        _logger.LogDebug(
            "template_contextual_source=liens_db_fallback TenantId={TenantId} contextType={ContextType} stageId={StageId}",
            tenantId, contextType, workflowStageId);

        var list = await _repo.GetActiveByTenantAsync(tenantId, contextType, workflowStageId, ct);
        return list.Select(MapToResponse).ToList();
    }

    private async Task<List<TaskTemplateResponse>?> TryGetContextualFromTaskServiceAsync(
        Guid    tenantId,
        string? contextType,
        Guid?   workflowStageId,
        CancellationToken ct)
    {
        try
        {
            var all = await _taskClient.GetAllTemplatesAsync(tenantId, ProductCode, ct);

            if (all.Count == 0)
            {
                _logger.LogDebug(
                    "template_contextual_source=task_service_empty TenantId={TenantId}; falling back to Liens DB.",
                    tenantId);
                return null;
            }

            var filtered = all
                .Select(dto => new { dto, ext = LiensTemplateExtensions.Deserialize(dto.ProductSettingsJson) })
                .Where(x => IsContextualMatch(x.dto, x.ext, contextType, workflowStageId))
                .OrderBy(x => ContextualSortOrder(x.ext.ContextType, contextType))
                .ThenBy(x => x.dto.Name)
                .Select(x => MapFromTaskServiceDto(x.dto))
                .ToList();

            return filtered;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "template_contextual_source=task_service_error TenantId={TenantId}; falling back to Liens DB.",
                tenantId);
            return null;
        }
    }

    private static bool IsContextualMatch(
        TaskServiceTemplateResponse dto,
        LiensTemplateExtensions     ext,
        string?                     contextType,
        Guid?                       workflowStageId)
    {
        if (!dto.IsActive) return false;
        if (string.IsNullOrWhiteSpace(contextType)) return true;

        var ct2 = ext.ContextType;
        return ct2 == TaskTemplateContextType.General
            || ct2 == contextType
            || (ct2 == TaskTemplateContextType.Stage
                && workflowStageId.HasValue
                && ext.ApplicableWorkflowStageId == workflowStageId);
    }

    private static int ContextualSortOrder(string ctxType, string? requestedContextType)
    {
        if (ctxType == TaskTemplateContextType.Stage)  return 0;
        if (ctxType == requestedContextType)           return 1;
        return 2;
    }

    /// <summary>
    /// TASK-MIG-07 — Admin get-by-id: Task service primary, Liens DB fallback.
    /// </summary>
    public async Task<TaskTemplateResponse?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        try
        {
            var dto = await _taskClient.GetTemplateAsync(tenantId, id, ct);
            if (dto is not null)
            {
                _logger.LogDebug(
                    "template_read_owner=task_service GetById TemplateId={TemplateId} TenantId={TenantId}",
                    id, tenantId);
                return MapFromTaskServiceDto(dto);
            }

            _logger.LogDebug(
                "template_read_owner=liens_db_fallback GetById TemplateId={TemplateId} TenantId={TenantId} (not found in Task service)",
                id, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "template_read_owner=liens_db_fallback GetById TemplateId={TemplateId} TenantId={TenantId} (Task service error)",
                id, tenantId);
        }

        var entity = await _repo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    // ── TASK-MIG-02 — Dual-read for generation engine (unchanged) ─────────────────

    public async Task<TaskTemplateResponse?> GetForGenerationAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var fromTask = await TryFetchFromTaskServiceAsync(tenantId, id, ct);
        if (fromTask is not null)
        {
            _logger.LogDebug(
                "template_source=task_service TemplateId={TemplateId} TenantId={TenantId}",
                id, tenantId);
            return fromTask;
        }

        var entity = await _repo.GetByIdAsync(tenantId, id, ct);
        if (entity is not null)
        {
            _logger.LogDebug(
                "template_source=liens_fallback TemplateId={TemplateId} TenantId={TenantId}",
                id, tenantId);
            return MapToResponse(entity);
        }

        return null;
    }

    private async Task<TaskTemplateResponse?> TryFetchFromTaskServiceAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _taskClient.GetTemplateAsync(tenantId, id, ct);
            return dto is null ? null : MapFromTaskServiceDto(dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "template_source=task_service_error TemplateId={TemplateId} TenantId={TenantId}; falling back to Liens DB.",
                id, tenantId);
            return null;
        }
    }

    // ── TASK-MIG-07 — Write operations: Task service PRIMARY, Liens DB mirror ──────

    /// <summary>
    /// TASK-MIG-07: Write to Task service first (primary owner). Mirror to Liens DB best-effort.
    /// A Task service write failure throws — not swallowed.
    /// A Liens DB mirror failure is logged and tolerated.
    /// template_write_owner=task_service
    /// </summary>
    public async Task<TaskTemplateResponse> CreateAsync(
        Guid tenantId, Guid actingUserId, CreateTaskTemplateRequest request, CancellationToken ct = default)
    {
        var errors = Validate(request.Name, request.DefaultTitle, request.ContextType, request.UpdateSource);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        // Build the domain entity in-memory to validate fields and generate a stable ID.
        // The entity is NOT saved to Liens DB first (ownership flip).
        var entity = LienTaskTemplate.Create(
            tenantId:                  tenantId,
            name:                      request.Name,
            defaultTitle:              request.DefaultTitle,
            defaultPriority:           request.DefaultPriority,
            contextType:               request.ContextType,
            updateSource:              request.UpdateSource,
            createdByUserId:           actingUserId,
            description:               request.Description,
            defaultDescription:        request.DefaultDescription,
            defaultDueOffsetDays:      request.DefaultDueOffsetDays,
            defaultRoleId:             request.DefaultRoleId,
            applicableWorkflowStageId: request.ApplicableWorkflowStageId,
            updatedByName:             request.UpdatedByName);

        // PRIMARY WRITE — Task service
        var payload = MapToUpsertPayload(entity);
        await _taskClient.UpsertTemplateFromSourceAsync(tenantId, actingUserId, payload, ct);

        _logger.LogInformation(
            "template_write_owner=task_service Created TemplateId={TemplateId} TenantId={TenantId}",
            entity.Id, tenantId);

        _audit.Publish(
            eventType:   "liens.task_template.created",
            action:      "create",
            description: $"Task template '{entity.Name}' created (Task service primary)",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    entity.Id.ToString());

        // MIRROR — Liens DB (best-effort rollback safety)
        await TryMirrorCreateToLiensDbAsync(tenantId, entity, ct);

        return MapToResponse(entity);
    }

    /// <summary>
    /// TASK-MIG-07: Task service primary write. Liens entity is still loaded for version
    /// conflict detection (transitional — Liens version is the client-visible version).
    /// template_write_owner=task_service
    /// </summary>
    public async Task<TaskTemplateResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId, UpdateTaskTemplateRequest request, CancellationToken ct = default)
    {
        // Load from Liens DB for version check (transitional authority for optimistic concurrency).
        var entity = await RequireTemplate(tenantId, id, ct);

        if (entity.Version != request.Version)
            throw new ConflictException(
                $"Stale version — expected {entity.Version}, got {request.Version}. Reload and retry.",
                "TASK_TEMPLATE_VERSION_CONFLICT");

        var errors = Validate(request.Name, request.DefaultTitle, request.ContextType, request.UpdateSource);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        // Apply changes in-memory (not saved to Liens DB yet).
        entity.Update(
            name:                      request.Name,
            description:               request.Description,
            defaultTitle:              request.DefaultTitle,
            defaultDescription:        request.DefaultDescription,
            defaultPriority:           request.DefaultPriority,
            defaultDueOffsetDays:      request.DefaultDueOffsetDays,
            defaultRoleId:             request.DefaultRoleId,
            contextType:               request.ContextType,
            applicableWorkflowStageId: request.ApplicableWorkflowStageId,
            updateSource:              request.UpdateSource,
            updatedByUserId:           actingUserId,
            expectedVersion:           request.Version,
            updatedByName:             request.UpdatedByName);

        // PRIMARY WRITE — Task service
        var payload = MapToUpsertPayload(entity);
        await _taskClient.UpsertTemplateFromSourceAsync(tenantId, actingUserId, payload, ct);

        _logger.LogInformation(
            "template_write_owner=task_service Updated TemplateId={TemplateId} TenantId={TenantId}",
            entity.Id, tenantId);

        _audit.Publish(
            eventType:   "liens.task_template.updated",
            action:      "update",
            description: $"Task template '{entity.Name}' updated (Task service primary)",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    entity.Id.ToString());

        // MIRROR — Liens DB (best-effort rollback safety)
        await TryMirrorUpdateToLiensDbAsync(tenantId, entity, ct);

        return MapToResponse(entity);
    }

    /// <summary>
    /// TASK-MIG-07: Task service primary write for activate.
    /// template_write_owner=task_service
    /// </summary>
    public async Task<TaskTemplateResponse> ActivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateTemplateRequest request, CancellationToken ct = default)
    {
        var entity = await RequireTemplate(tenantId, id, ct);

        if (!WorkflowUpdateSources.All.Contains(request.UpdateSource))
            throw new ValidationException("Validation failed.",
                new Dictionary<string, string[]> { { "updateSource", [$"Invalid updateSource '{request.UpdateSource}'."] } });

        // Apply change in-memory (not saved to Liens DB yet).
        entity.Activate(actingUserId, request.UpdateSource, request.UpdatedByName);

        // PRIMARY WRITE — Task service
        var payload = MapToUpsertPayload(entity);
        await _taskClient.UpsertTemplateFromSourceAsync(tenantId, actingUserId, payload, ct);

        _logger.LogInformation(
            "template_write_owner=task_service Activated TemplateId={TemplateId} TenantId={TenantId}",
            entity.Id, tenantId);

        _audit.Publish(
            eventType:   "liens.task_template.activated",
            action:      "activate",
            description: $"Task template '{entity.Name}' activated (Task service primary)",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    entity.Id.ToString());

        // MIRROR — Liens DB (best-effort rollback safety)
        await TryMirrorUpdateToLiensDbAsync(tenantId, entity, ct);

        return MapToResponse(entity);
    }

    /// <summary>
    /// TASK-MIG-07: Task service primary write for deactivate.
    /// template_write_owner=task_service
    /// </summary>
    public async Task<TaskTemplateResponse> DeactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateTemplateRequest request, CancellationToken ct = default)
    {
        var entity = await RequireTemplate(tenantId, id, ct);

        if (!WorkflowUpdateSources.All.Contains(request.UpdateSource))
            throw new ValidationException("Validation failed.",
                new Dictionary<string, string[]> { { "updateSource", [$"Invalid updateSource '{request.UpdateSource}'."] } });

        // Apply change in-memory (not saved to Liens DB yet).
        entity.Deactivate(actingUserId, request.UpdateSource, request.UpdatedByName);

        // PRIMARY WRITE — Task service
        var payload = MapToUpsertPayload(entity);
        await _taskClient.UpsertTemplateFromSourceAsync(tenantId, actingUserId, payload, ct);

        _logger.LogInformation(
            "template_write_owner=task_service Deactivated TemplateId={TemplateId} TenantId={TenantId}",
            entity.Id, tenantId);

        _audit.Publish(
            eventType:   "liens.task_template.deactivated",
            action:      "deactivate",
            description: $"Task template '{entity.Name}' deactivated (Task service primary)",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    entity.Id.ToString());

        // MIRROR — Liens DB (best-effort rollback safety)
        await TryMirrorUpdateToLiensDbAsync(tenantId, entity, ct);

        return MapToResponse(entity);
    }

    // ── Mirror helpers (Liens DB rollback safety — non-authoritative) ─────────────

    private async Task TryMirrorCreateToLiensDbAsync(
        Guid tenantId, LienTaskTemplate entity, CancellationToken ct)
    {
        try
        {
            await _repo.AddAsync(entity, ct);
            _logger.LogDebug(
                "template_mirror_target=liens_db Create TemplateId={TemplateId} TenantId={TenantId}",
                entity.Id, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "template_mirror_target=liens_db_failed Create TemplateId={TemplateId} TenantId={TenantId}; "
                + "Task service write succeeded — Liens DB mirror is non-authoritative.",
                entity.Id, tenantId);
        }
    }

    private async Task TryMirrorUpdateToLiensDbAsync(
        Guid tenantId, LienTaskTemplate entity, CancellationToken ct)
    {
        try
        {
            await _repo.UpdateAsync(entity, ct);
            _logger.LogDebug(
                "template_mirror_target=liens_db Update TemplateId={TemplateId} TenantId={TenantId}",
                entity.Id, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "template_mirror_target=liens_db_failed Update TemplateId={TemplateId} TenantId={TenantId}; "
                + "Task service write succeeded — Liens DB mirror is non-authoritative.",
                entity.Id, tenantId);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<LienTaskTemplate> RequireTemplate(Guid tenantId, Guid id, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct);
        if (entity is null)
            throw new NotFoundException($"Task template '{id}' not found.");
        return entity;
    }

    private static Dictionary<string, string[]> Validate(
        string name, string defaultTitle, string contextType, string updateSource)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name))
            errors.Add("name", ["Name is required."]);
        if (string.IsNullOrWhiteSpace(defaultTitle))
            errors.Add("defaultTitle", ["DefaultTitle is required."]);
        if (!TaskTemplateContextType.All.Contains(contextType))
            errors.Add("contextType", [$"Invalid contextType '{contextType}'."]);
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            errors.Add("updateSource", [$"Invalid updateSource '{updateSource}'."]);
        return errors;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the Task service upsert payload from a Liens domain entity.
    /// Liens-specific fields (ContextType, ApplicableWorkflowStageId, DefaultRoleId)
    /// are packed into ProductSettingsJson.
    /// </summary>
    public static TaskServiceTemplateUpsertRequest MapToUpsertPayload(LienTaskTemplate entity)
    {
        var ext = new LiensTemplateExtensions
        {
            ContextType               = entity.ContextType,
            ApplicableWorkflowStageId = entity.ApplicableWorkflowStageId,
            DefaultRoleId             = entity.DefaultRoleId,
        };

        return new TaskServiceTemplateUpsertRequest
        {
            Id                  = entity.Id,
            Code                = entity.Id.ToString("N").ToUpperInvariant(),
            Name                = entity.Name,
            DefaultTitle        = entity.DefaultTitle,
            SourceProductCode   = ProductCode,
            Description         = entity.Description,
            DefaultDescription  = entity.DefaultDescription,
            DefaultPriority     = entity.DefaultPriority,
            DefaultScope        = DefaultScope,
            DefaultDueInDays    = entity.DefaultDueOffsetDays,
            DefaultStageId      = null,
            IsActive            = entity.IsActive,
            ProductSettingsJson = ext.Serialize(),
        };
    }

    private static TaskTemplateResponse MapToResponse(LienTaskTemplate e) => new()
    {
        Id                        = e.Id,
        TenantId                  = e.TenantId,
        ProductCode               = e.ProductCode,
        Name                      = e.Name,
        Description               = e.Description,
        DefaultTitle              = e.DefaultTitle,
        DefaultDescription        = e.DefaultDescription,
        DefaultPriority           = e.DefaultPriority,
        DefaultDueOffsetDays      = e.DefaultDueOffsetDays,
        DefaultRoleId             = e.DefaultRoleId,
        ContextType               = e.ContextType,
        ApplicableWorkflowStageId = e.ApplicableWorkflowStageId,
        IsActive                  = e.IsActive,
        Version                   = e.Version,
        LastUpdatedAt             = e.LastUpdatedAt,
        LastUpdatedByUserId       = e.LastUpdatedByUserId,
        LastUpdatedByName         = e.LastUpdatedByName,
        LastUpdatedSource         = e.LastUpdatedSource,
        CreatedAtUtc              = e.CreatedAtUtc,
        UpdatedAtUtc              = e.UpdatedAtUtc,
    };

    private static TaskTemplateResponse MapFromTaskServiceDto(TaskServiceTemplateResponse dto)
    {
        var ext = LiensTemplateExtensions.Deserialize(dto.ProductSettingsJson);

        return new TaskTemplateResponse
        {
            Id                        = dto.Id,
            TenantId                  = dto.TenantId,
            ProductCode               = ProductCode,
            Name                      = dto.Name,
            Description               = dto.Description,
            DefaultTitle              = dto.DefaultTitle,
            DefaultDescription        = dto.DefaultDescription,
            DefaultPriority           = dto.DefaultPriority,
            DefaultDueOffsetDays      = dto.DefaultDueInDays,
            DefaultRoleId             = ext.DefaultRoleId,
            ContextType               = ext.ContextType,
            ApplicableWorkflowStageId = ext.ApplicableWorkflowStageId,
            IsActive                  = dto.IsActive,
            Version                   = dto.Version,
            LastUpdatedAt             = dto.UpdatedAtUtc,
            LastUpdatedByUserId       = null,
            LastUpdatedByName         = null,
            LastUpdatedSource         = WorkflowUpdateSources.TaskServiceSync,
            CreatedAtUtc              = dto.CreatedAtUtc,
            UpdatedAtUtc              = dto.UpdatedAtUtc,
        };
    }
}
