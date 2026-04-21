using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienTaskTemplateService : ILienTaskTemplateService
{
    private const string ProductCode = "SYNQ_LIENS";
    private const string DefaultScope = "GENERAL";

    private readonly ILienTaskTemplateRepository     _repo;
    private readonly ILiensTaskServiceClient         _taskClient;
    private readonly IAuditPublisher                 _audit;
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

    // ── Admin / tenant reads (always Liens DB — no dual-read for admin) ───────────

    public async Task<List<TaskTemplateResponse>> GetByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var list = await _repo.GetByTenantAsync(tenantId, ct);
        return list.Select(MapToResponse).ToList();
    }

    public async Task<List<TaskTemplateResponse>> GetContextualAsync(
        Guid tenantId, string? contextType, Guid? workflowStageId, CancellationToken ct = default)
    {
        var list = await _repo.GetActiveByTenantAsync(tenantId, contextType, workflowStageId, ct);
        return list.Select(MapToResponse).ToList();
    }

    public async Task<TaskTemplateResponse?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    // ── TASK-MIG-02 — Dual-read for generation engine ────────────────────────────

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

    // ── Write operations (Liens DB authoritative + best-effort Task sync) ─────────

    public async Task<TaskTemplateResponse> CreateAsync(
        Guid tenantId, Guid actingUserId, CreateTaskTemplateRequest request, CancellationToken ct = default)
    {
        var errors = Validate(request.Name, request.DefaultTitle, request.ContextType, request.UpdateSource);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

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

        await _repo.AddAsync(entity, ct);

        _logger.LogInformation("TaskTemplate created: {TemplateId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType:   "liens.task_template.created",
            action:      "create",
            description: $"Task template '{entity.Name}' created from {request.UpdateSource}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    entity.Id.ToString());

        await TrySyncToTaskServiceAsync(tenantId, actingUserId, entity, "template_sync_create");

        return MapToResponse(entity);
    }

    public async Task<TaskTemplateResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId, UpdateTaskTemplateRequest request, CancellationToken ct = default)
    {
        var entity = await RequireTemplate(tenantId, id, ct);

        if (entity.Version != request.Version)
            throw new ConflictException(
                $"Stale version — expected {entity.Version}, got {request.Version}. Reload and retry.",
                "TASK_TEMPLATE_VERSION_CONFLICT");

        var errors = Validate(request.Name, request.DefaultTitle, request.ContextType, request.UpdateSource);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

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

        await _repo.UpdateAsync(entity, ct);

        _logger.LogInformation("TaskTemplate updated: {TemplateId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType:   "liens.task_template.updated",
            action:      "update",
            description: $"Task template '{entity.Name}' updated from {request.UpdateSource}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    entity.Id.ToString());

        await TrySyncToTaskServiceAsync(tenantId, actingUserId, entity, "template_sync_update");

        return MapToResponse(entity);
    }

    public async Task<TaskTemplateResponse> ActivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateTemplateRequest request, CancellationToken ct = default)
    {
        var entity = await RequireTemplate(tenantId, id, ct);

        if (!WorkflowUpdateSources.All.Contains(request.UpdateSource))
            throw new ValidationException("Validation failed.",
                new Dictionary<string, string[]> { { "updateSource", [$"Invalid updateSource '{request.UpdateSource}'."] } });

        entity.Activate(actingUserId, request.UpdateSource, request.UpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.task_template.activated",
            action:      "activate",
            description: $"Task template '{entity.Name}' activated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    entity.Id.ToString());

        await TrySyncToTaskServiceAsync(tenantId, actingUserId, entity, "template_sync_activate");

        return MapToResponse(entity);
    }

    public async Task<TaskTemplateResponse> DeactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateTemplateRequest request, CancellationToken ct = default)
    {
        var entity = await RequireTemplate(tenantId, id, ct);

        if (!WorkflowUpdateSources.All.Contains(request.UpdateSource))
            throw new ValidationException("Validation failed.",
                new Dictionary<string, string[]> { { "updateSource", [$"Invalid updateSource '{request.UpdateSource}'."] } });

        entity.Deactivate(actingUserId, request.UpdateSource, request.UpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.task_template.deactivated",
            action:      "deactivate",
            description: $"Task template '{entity.Name}' deactivated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    entity.Id.ToString());

        await TrySyncToTaskServiceAsync(tenantId, actingUserId, entity, "template_sync_deactivate");

        return MapToResponse(entity);
    }

    // ── Write-through sync ────────────────────────────────────────────────────────

    private async Task TrySyncToTaskServiceAsync(
        Guid tenantId, Guid actingUserId, LienTaskTemplate entity, string operation)
    {
        try
        {
            var payload = MapToUpsertPayload(entity);
            await _taskClient.UpsertTemplateFromSourceAsync(tenantId, actingUserId, payload);
            _logger.LogDebug(
                "template_sync=ok Operation={Operation} TemplateId={TemplateId} TenantId={TenantId}",
                operation, entity.Id, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "template_sync=failed Operation={Operation} TemplateId={TemplateId} TenantId={TenantId}; "
                + "startup sync will reconcile.",
                operation, entity.Id, tenantId);
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
