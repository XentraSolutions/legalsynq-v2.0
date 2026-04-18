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
    private readonly ILienTaskTemplateRepository _repo;
    private readonly IAuditPublisher             _audit;
    private readonly ILogger<LienTaskTemplateService> _logger;

    public LienTaskTemplateService(
        ILienTaskTemplateRepository repo,
        IAuditPublisher audit,
        ILogger<LienTaskTemplateService> logger)
    {
        _repo   = repo;
        _audit  = audit;
        _logger = logger;
    }

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

        return MapToResponse(entity);
    }

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
}
