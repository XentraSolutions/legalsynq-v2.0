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
/// LS-LIENS-FLOW-006 — Manages task governance settings for a tenant.
/// Follows the same governance pattern as LienWorkflowConfigService.
/// </summary>
public sealed class LienTaskGovernanceService : ILienTaskGovernanceService
{
    private readonly ILienTaskGovernanceSettingsRepository _repo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<LienTaskGovernanceService> _logger;

    public LienTaskGovernanceService(
        ILienTaskGovernanceSettingsRepository repo,
        IAuditPublisher audit,
        ILogger<LienTaskGovernanceService> logger)
    {
        _repo  = repo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<TaskGovernanceSettingsResponse> GetOrCreateAsync(
        Guid tenantId, Guid actingUserId, string updateSource, CancellationToken ct = default)
    {
        var entity = await _repo.GetByTenantProductAsync(tenantId, LiensPermissions.ProductCode, ct);
        if (entity is not null)
            return MapToResponse(entity);

        var newEntity = LienTaskGovernanceSettings.CreateDefault(
            tenantId:      tenantId,
            productCode:   LiensPermissions.ProductCode,
            updateSource:  updateSource,
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
            "Task governance settings created for tenant {TenantId} with defaults.", tenantId);

        return MapToResponse(newEntity);
    }

    public async Task<TaskGovernanceSettingsResponse> UpdateAsync(
        Guid tenantId, Guid actingUserId,
        UpdateTaskGovernanceSettingsRequest request, CancellationToken ct = default)
    {
        var entity = await _repo.GetByTenantProductAsync(tenantId, LiensPermissions.ProductCode, ct);

        if (entity is null)
        {
            entity = LienTaskGovernanceSettings.CreateDefault(
                tenantId:        tenantId,
                productCode:     LiensPermissions.ProductCode,
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

        return MapToResponse(entity);
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
