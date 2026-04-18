using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienWorkflowConfigService : ILienWorkflowConfigService
{
    private const string ProductCode = "SYNQ_LIENS";

    private readonly ILienWorkflowConfigRepository _repo;
    private readonly IAuditPublisher               _audit;
    private readonly ILogger<LienWorkflowConfigService> _logger;

    public LienWorkflowConfigService(
        ILienWorkflowConfigRepository repo,
        IAuditPublisher audit,
        ILogger<LienWorkflowConfigService> logger)
    {
        _repo   = repo;
        _audit  = audit;
        _logger = logger;
    }

    public async Task<WorkflowConfigResponse?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByTenantProductAsync(tenantId, ProductCode, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<WorkflowConfigResponse> CreateAsync(
        Guid tenantId, Guid actingUserId, CreateWorkflowConfigRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.WorkflowName))
            errors.Add("workflowName", ["WorkflowName is required."]);
        if (!WorkflowUpdateSources.All.Contains(request.UpdateSource))
            errors.Add("updateSource", [$"Invalid updateSource '{request.UpdateSource}'."]);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        var existing = await _repo.GetByTenantProductAsync(tenantId, ProductCode, ct);
        if (existing is not null)
            throw new ConflictException(
                $"Workflow config for tenant already exists. Use PUT to update.",
                "WORKFLOW_CONFIG_EXISTS");

        var entity = LienWorkflowConfig.Create(
            tenantId:      tenantId,
            productCode:   ProductCode,
            workflowName:  request.WorkflowName,
            updateSource:  request.UpdateSource,
            createdByUserId: actingUserId,
            updatedByName: request.UpdatedByName);

        await _repo.AddAsync(entity, ct);

        _logger.LogInformation("WorkflowConfig created: {ConfigId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType:   "liens.workflow_config.created",
            action:      "create",
            description: $"Workflow '{entity.WorkflowName}' created from {request.UpdateSource}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowConfig",
            entityId:    entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<WorkflowConfigResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId, UpdateWorkflowConfigRequest request, CancellationToken ct = default)
    {
        var entity = await RequireConfig(tenantId, id, ct);

        if (entity.Version != request.Version)
            throw new ConflictException(
                $"Stale version — expected {entity.Version}, got {request.Version}. Reload and retry.",
                "WORKFLOW_CONFIG_VERSION_CONFLICT");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.WorkflowName))
            errors.Add("workflowName", ["WorkflowName is required."]);
        if (!WorkflowUpdateSources.All.Contains(request.UpdateSource))
            errors.Add("updateSource", [$"Invalid updateSource '{request.UpdateSource}'."]);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        entity.Update(request.WorkflowName, request.IsActive, request.UpdateSource, actingUserId, request.UpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.workflow_config.updated",
            action:      "update",
            description: $"Workflow '{entity.WorkflowName}' updated from {request.UpdateSource}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowConfig",
            entityId:    entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<WorkflowConfigResponse> AddStageAsync(
        Guid tenantId, Guid id, Guid actingUserId, AddWorkflowStageRequest request, CancellationToken ct = default)
    {
        var entity = await RequireConfig(tenantId, id, ct);

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.StageName))
            errors.Add("stageName", ["StageName is required."]);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        var stage = LienWorkflowStage.Create(
            entity.Id, request.StageName, request.StageOrder, actingUserId,
            request.Description, request.DefaultOwnerRole, request.SlaMetadata);

        await _repo.AddStageAsync(stage, ct);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.workflow_stage.added",
            action:      "create",
            description: $"Stage '{stage.StageName}' added to workflow '{entity.WorkflowName}'",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowStage",
            entityId:    stage.Id.ToString());

        var updated = await _repo.GetByIdAsync(tenantId, id, ct);
        return MapToResponse(updated!);
    }

    public async Task<WorkflowConfigResponse> UpdateStageAsync(
        Guid tenantId, Guid id, Guid stageId, Guid actingUserId, UpdateWorkflowStageRequest request, CancellationToken ct = default)
    {
        await RequireConfig(tenantId, id, ct);
        var stage = await _repo.GetStageByIdAsync(id, stageId, ct)
            ?? throw new NotFoundException($"Stage '{stageId}' not found.");

        if (string.IsNullOrWhiteSpace(request.StageName))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
            {
                ["stageName"] = ["StageName is required."]
            });

        stage.Update(request.StageName, request.StageOrder, request.IsActive, actingUserId,
            request.Description, request.DefaultOwnerRole, request.SlaMetadata);

        await _repo.UpdateStageAsync(stage, ct);

        _audit.Publish(
            eventType:   "liens.workflow_stage.updated",
            action:      "update",
            description: $"Stage '{stage.StageName}' updated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowStage",
            entityId:    stage.Id.ToString());

        var updated = await _repo.GetByIdAsync(tenantId, id, ct);
        return MapToResponse(updated!);
    }

    public async Task<WorkflowConfigResponse> RemoveStageAsync(
        Guid tenantId, Guid id, Guid stageId, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await RequireConfig(tenantId, id, ct);
        var stage  = await _repo.GetStageByIdAsync(id, stageId, ct)
            ?? throw new NotFoundException($"Stage '{stageId}' not found.");

        stage.Deactivate(actingUserId);
        await _repo.UpdateStageAsync(stage, ct);

        entity.Update(entity.WorkflowName, entity.IsActive, entity.LastUpdatedSource, actingUserId, entity.LastUpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.workflow_stage.deactivated",
            action:      "update",
            description: $"Stage '{stage.StageName}' deactivated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowStage",
            entityId:    stage.Id.ToString());

        var updated = await _repo.GetByIdAsync(tenantId, id, ct);
        return MapToResponse(updated!);
    }

    public async Task<WorkflowConfigResponse> ReorderStagesAsync(
        Guid tenantId, Guid id, Guid actingUserId, ReorderStagesRequest request, CancellationToken ct = default)
    {
        var entity = await RequireConfig(tenantId, id, ct);

        foreach (var entry in request.Stages)
        {
            var stage = await _repo.GetStageByIdAsync(id, entry.StageId, ct);
            if (stage is null) continue;
            stage.Update(stage.StageName, entry.StageOrder, stage.IsActive, actingUserId,
                stage.Description, stage.DefaultOwnerRole, stage.SlaMetadata);
            await _repo.UpdateStageAsync(stage, ct);
        }

        entity.Update(entity.WorkflowName, entity.IsActive, entity.LastUpdatedSource, actingUserId, entity.LastUpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.workflow_stage.reordered",
            action:      "update",
            description: $"Workflow '{entity.WorkflowName}' stages reordered",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowConfig",
            entityId:    entity.Id.ToString());

        var updated = await _repo.GetByIdAsync(tenantId, id, ct);
        return MapToResponse(updated!);
    }

    private async Task<LienWorkflowConfig> RequireConfig(Guid tenantId, Guid id, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct);
        if (entity is null)
            throw new NotFoundException($"WorkflowConfig '{id}' not found.");
        return entity;
    }

    private static WorkflowConfigResponse MapToResponse(LienWorkflowConfig entity)
    {
        return new WorkflowConfigResponse
        {
            Id                  = entity.Id,
            TenantId            = entity.TenantId,
            ProductCode         = entity.ProductCode,
            WorkflowName        = entity.WorkflowName,
            Version             = entity.Version,
            IsActive            = entity.IsActive,
            LastUpdatedAt       = entity.LastUpdatedAt,
            LastUpdatedByUserId = entity.LastUpdatedByUserId,
            LastUpdatedByName   = entity.LastUpdatedByName,
            LastUpdatedSource   = entity.LastUpdatedSource,
            CreatedAtUtc        = entity.CreatedAtUtc,
            UpdatedAtUtc        = entity.UpdatedAtUtc,
            Stages = entity.Stages
                .OrderBy(s => s.StageOrder)
                .Select(s => new WorkflowStageResponse
                {
                    Id               = s.Id,
                    WorkflowConfigId = s.WorkflowConfigId,
                    StageName        = s.StageName,
                    StageOrder       = s.StageOrder,
                    Description      = s.Description,
                    IsActive         = s.IsActive,
                    DefaultOwnerRole = s.DefaultOwnerRole,
                    SlaMetadata      = s.SlaMetadata,
                    CreatedAtUtc     = s.CreatedAtUtc,
                    UpdatedAtUtc     = s.UpdatedAtUtc,
                }).ToList(),
        };
    }
}
