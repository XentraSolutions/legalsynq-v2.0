using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienTaskService : ILienTaskService
{
    private readonly ILienTaskRepository    _taskRepo;
    private readonly ICaseRepository        _caseRepo;
    private readonly ILienRepository        _lienRepo;
    private readonly IAuditPublisher        _audit;
    private readonly INotificationPublisher _notifications;
    private readonly ILienWorkflowConfigRepository         _workflowRepo;
    private readonly IWorkflowTransitionValidationService  _transitionValidator;
    // LS-LIENS-FLOW-006 — governance enforcement
    private readonly ILienTaskGovernanceSettingsRepository _governanceRepo;
    // LS-LIENS-FLOW-007 — Flow instance linkage resolver
    private readonly IFlowInstanceResolver _flowResolver;
    private readonly ILogger<LienTaskService> _logger;

    public LienTaskService(
        ILienTaskRepository taskRepo,
        ICaseRepository caseRepo,
        ILienRepository lienRepo,
        IAuditPublisher audit,
        INotificationPublisher notifications,
        ILienWorkflowConfigRepository workflowRepo,
        IWorkflowTransitionValidationService transitionValidator,
        ILienTaskGovernanceSettingsRepository governanceRepo,
        IFlowInstanceResolver flowResolver,
        ILogger<LienTaskService> logger)
    {
        _taskRepo            = taskRepo;
        _caseRepo            = caseRepo;
        _lienRepo            = lienRepo;
        _audit               = audit;
        _notifications       = notifications;
        _workflowRepo        = workflowRepo;
        _transitionValidator = transitionValidator;
        _governanceRepo      = governanceRepo;
        _flowResolver        = flowResolver;
        _logger              = logger;
    }

    public async Task<PaginatedResult<TaskResponse>> SearchAsync(
        Guid tenantId,
        string? search,
        string? status,
        string? priority,
        Guid? assignedUserId,
        Guid? caseId,
        Guid? lienId,
        Guid? workflowStageId,
        string? assignmentScope,
        Guid? currentUserId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var (items, totalCount) = await _taskRepo.SearchAsync(
            tenantId, search, status, priority, assignedUserId, caseId, lienId,
            workflowStageId, assignmentScope, currentUserId, page, pageSize, ct);

        var responses = new List<TaskResponse>(items.Count);
        foreach (var item in items)
        {
            var links = await _taskRepo.GetLienLinksForTaskAsync(item.Id, ct);
            responses.Add(MapToResponse(item, links));
        }

        return new PaginatedResult<TaskResponse>
        {
            Items      = responses,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<TaskResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _taskRepo.GetByIdAsync(tenantId, id, ct);
        if (entity is null) return null;

        var links = await _taskRepo.GetLienLinksForTaskAsync(id, ct);
        return MapToResponse(entity, links);
    }

    public async Task<TaskResponse> CreateAsync(
        Guid tenantId, Guid actingUserId, CreateTaskRequest request, CancellationToken ct = default)
    {
        // ── Basic field validation ─────────────────────────────────────────────
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
            errors.Add("title", ["Title is required."]);
        if (request.Priority is not null && !TaskPriorities.All.Contains(request.Priority))
            errors.Add("priority", [$"Invalid priority '{request.Priority}'."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing.", errors);

        // ── LS-LIENS-FLOW-006: Governance enforcement ─────────────────────────
        // System-generated tasks still go through governance — the engine must
        // satisfy rules or the task is skipped with an audit log entry.
        var governance = await _governanceRepo.GetByTenantProductAsync(
            tenantId, LiensPermissions.ProductCode, ct);

        var effectiveAssignedUserId = request.AssignedUserId;
        var effectiveCaseId         = request.CaseId;
        var effectiveWorkflowStageId = request.WorkflowStageId;

        if (governance is not null)
        {
            // 1. Assignee requirement
            if (governance.RequireAssigneeOnCreate && !effectiveAssignedUserId.HasValue)
                errors.Add("assignedUserId", ["Task assignee is required."]);

            // 2. Case link requirement
            if (governance.RequireCaseLinkOnCreate && !effectiveCaseId.HasValue)
                errors.Add("caseId", ["Task must be linked to a case."]);

            // 3. Workflow stage requirement — auto-derive when not provided
            if (governance.RequireWorkflowStageOnCreate && !effectiveWorkflowStageId.HasValue)
            {
                effectiveWorkflowStageId = await DeriveStartStageAsync(tenantId, governance, ct);
                if (!effectiveWorkflowStageId.HasValue)
                    errors.Add("workflowStageId", ["A valid workflow stage is required for task creation. Configure at least one active stage in your workflow settings."]);
            }

            if (errors.Count > 0)
                throw new ValidationException("Task creation does not satisfy governance requirements.", errors);
        }

        // ── Create entity ──────────────────────────────────────────────────────
        var entity = LienTask.Create(
            tenantId:              tenantId,
            title:                 request.Title,
            createdByUserId:       actingUserId,
            description:           request.Description,
            priority:              request.Priority,
            assignedUserId:        effectiveAssignedUserId,
            caseId:                effectiveCaseId,
            workflowStageId:       effectiveWorkflowStageId,
            dueDate:               request.DueDate,
            sourceType:            request.SourceType,
            generationRuleId:      request.GenerationRuleId,
            generatingTemplateId:  request.GeneratingTemplateId);

        await _taskRepo.AddAsync(entity, ct);

        var links = await SaveLienLinksAsync(entity.Id, request.LienIds, actingUserId, ct);

        // ── LS-LIENS-FLOW-007: Attempt to link the active Flow workflow instance ────
        // Non-blocking: any Flow lookup failure is caught inside the resolver.
        // The task is already persisted — this is a best-effort enrichment step.
        if (entity.CaseId.HasValue)
        {
            var (instanceId, stepKey) = await _flowResolver.ResolveAsync(entity.CaseId.Value, ct);
            if (instanceId.HasValue)
            {
                entity.SetWorkflowLink(instanceId.Value, stepKey);
                await _taskRepo.UpdateAsync(entity, ct);

                _audit.Publish(
                    eventType:   "liens.task.workflow_linked",
                    action:      "update",
                    description: $"Task '{entity.Title}' linked to Flow instance {instanceId.Value} (step: {stepKey ?? "N/A"})",
                    tenantId:    tenantId,
                    actorUserId: actingUserId,
                    entityType:  "LienTask",
                    entityId:    entity.Id.ToString());
            }
        }

        _logger.LogInformation("Task created: {TaskId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType:   "liens.task.created",
            action:      "create",
            description: $"Task '{entity.Title}' created",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTask",
            entityId:    entity.Id.ToString());

        await PublishCaseAuditAsync(entity, links, "liens.task.created", "create", $"Task '{entity.Title}' created", actingUserId, ct);

        // LS-LIENS-FLOW-006: Use distinct event key for create-with-assignee
        // to prevent template confusion with standalone assignment emails.
        if (entity.AssignedUserId.HasValue)
        {
            _ = _notifications.PublishAsync("liens.task.created_assigned", tenantId, new Dictionary<string, string>
            {
                ["tenantId"]        = tenantId.ToString(),
                ["taskId"]          = entity.Id.ToString(),
                ["taskTitle"]       = entity.Title,
                ["assignedTo"]      = entity.AssignedUserId.Value.ToString(),
                ["assignedBy"]      = actingUserId.ToString(),
                ["caseId"]          = entity.CaseId?.ToString() ?? string.Empty,
                ["lienIds"]         = string.Join(",", links.Select(l => l.LienId.ToString())),
                ["priority"]        = entity.Priority,
                ["workflowStageId"] = entity.WorkflowStageId?.ToString() ?? string.Empty,
                ["dueDate"]         = entity.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                ["sourceType"]      = entity.SourceType ?? string.Empty,
            }, ct);
        }

        return MapToResponse(entity, links);
    }

    public async Task<TaskResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var entity = await RequireTask(tenantId, id, ct);

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
            errors.Add("title", ["Title is required."]);
        if (request.Priority is not null && !TaskPriorities.All.Contains(request.Priority))
            errors.Add("priority", [$"Invalid priority '{request.Priority}'."]);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        // ── LS-LIENS-FLOW-005: Validate My Tasks stage transition ─────────────
        // Governs task-stage movement within My Tasks only.
        // This does NOT validate case or lien Flow workflow instance transitions —
        // those are owned by the Flow service (IFlowClient, WorkflowEndpoints.cs).
        // Transitional architecture: LS-LIENS-FLOW-007 will correlate this check
        // with the active Flow WorkflowInstance for the task's linked case.
        // Only validate when moving from one stage to a different stage (both non-null).
        if (entity.WorkflowStageId.HasValue
            && request.WorkflowStageId.HasValue
            && entity.WorkflowStageId.Value != request.WorkflowStageId.Value)
        {
            var fromStage = await _workflowRepo.GetStageGlobalAsync(entity.WorkflowStageId.Value, ct);
            if (fromStage is not null)
            {
                var allowed = await _transitionValidator.IsTransitionAllowedAsync(
                    fromStage.WorkflowConfigId,
                    entity.WorkflowStageId.Value,
                    request.WorkflowStageId.Value,
                    ct);

                if (!allowed)
                {
                    var toStage = await _workflowRepo.GetStageGlobalAsync(request.WorkflowStageId.Value, ct);
                    var fromName = fromStage.StageName;
                    var toName   = toStage?.StageName ?? request.WorkflowStageId.Value.ToString();
                    throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
                    {
                        ["workflowStageId"] = [
                            $"Transition from '{fromName}' to '{toName}' is not allowed by the workflow configuration."
                        ]
                    });
                }
            }
        }

        entity.Update(
            title:           request.Title,
            updatedByUserId: actingUserId,
            description:     request.Description,
            priority:        request.Priority,
            caseId:          request.CaseId,
            workflowStageId: request.WorkflowStageId,
            dueDate:         request.DueDate);

        await _taskRepo.UpdateAsync(entity, ct);

        await _taskRepo.RemoveLienLinksAsync(id, ct);
        var links = await SaveLienLinksAsync(id, request.LienIds, actingUserId, ct);

        _audit.Publish(
            eventType:   "liens.task.updated",
            action:      "update",
            description: $"Task '{entity.Title}' updated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTask",
            entityId:    entity.Id.ToString());

        await PublishCaseAuditAsync(entity, links, "liens.task.updated", "update", $"Task '{entity.Title}' updated", actingUserId, ct);

        return MapToResponse(entity, links);
    }

    public async Task<TaskResponse> AssignAsync(
        Guid tenantId, Guid id, Guid actingUserId, AssignTaskRequest request, CancellationToken ct = default)
    {
        var entity = await RequireTask(tenantId, id, ct);
        var previousAssignee = entity.AssignedUserId;

        entity.Assign(request.AssignedUserId, actingUserId);
        await _taskRepo.UpdateAsync(entity, ct);

        var links = await _taskRepo.GetLienLinksForTaskAsync(id, ct);

        var isReassignment = previousAssignee.HasValue && request.AssignedUserId.HasValue &&
                             previousAssignee.Value != request.AssignedUserId.Value;

        _audit.Publish(
            eventType:   "liens.task.assigned",
            action:      "update",
            description: $"Task '{entity.Title}' {(isReassignment ? "reassigned" : "assigned")}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTask",
            entityId:    entity.Id.ToString());

        await PublishCaseAuditAsync(entity, links, "liens.task.assigned", "update",
            $"Task '{entity.Title}' {(isReassignment ? "reassigned" : "assigned")}", actingUserId, ct);

        if (request.AssignedUserId.HasValue)
        {
            var notifKey = isReassignment ? "liens.task.reassigned" : "liens.task.assigned";
            _ = _notifications.PublishAsync(notifKey, tenantId, new Dictionary<string, string>
            {
                ["tenantId"]            = tenantId.ToString(),
                ["taskId"]              = entity.Id.ToString(),
                ["taskTitle"]           = entity.Title,
                ["assignedTo"]          = request.AssignedUserId.Value.ToString(),
                ["assignedBy"]          = actingUserId.ToString(),
                ["previousAssigneeId"]  = previousAssignee?.ToString() ?? string.Empty,
                ["caseId"]              = entity.CaseId?.ToString() ?? string.Empty,
                ["lienIds"]             = string.Join(",", links.Select(l => l.LienId.ToString())),
                ["priority"]            = entity.Priority,
                ["workflowStageId"]     = entity.WorkflowStageId?.ToString() ?? string.Empty,
                ["dueDate"]             = entity.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            }, ct);
        }

        return MapToResponse(entity, links);
    }

    public async Task<TaskResponse> UpdateStatusAsync(
        Guid tenantId, Guid id, Guid actingUserId, UpdateTaskStatusRequest request, CancellationToken ct = default)
    {
        if (!TaskStatuses.All.Contains(request.Status))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
            {
                ["status"] = [$"Invalid status '{request.Status}'."]
            });

        var entity = await RequireTask(tenantId, id, ct);
        entity.TransitionStatus(request.Status, actingUserId);
        await _taskRepo.UpdateAsync(entity, ct);

        var links = await _taskRepo.GetLienLinksForTaskAsync(id, ct);

        _audit.Publish(
            eventType:   "liens.task.status_changed",
            action:      "update",
            description: $"Task '{entity.Title}' status changed to {request.Status}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTask",
            entityId:    entity.Id.ToString());

        await PublishCaseAuditAsync(entity, links, "liens.task.status_changed", "update",
            $"Task '{entity.Title}' status → {request.Status}", actingUserId, ct);

        return MapToResponse(entity, links);
    }

    public async Task<TaskResponse> CompleteAsync(Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await RequireTask(tenantId, id, ct);
        entity.Complete(actingUserId);
        await _taskRepo.UpdateAsync(entity, ct);

        var links = await _taskRepo.GetLienLinksForTaskAsync(id, ct);

        _audit.Publish(
            eventType:   "liens.task.completed",
            action:      "update",
            description: $"Task '{entity.Title}' completed",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTask",
            entityId:    entity.Id.ToString());

        await PublishCaseAuditAsync(entity, links, "liens.task.completed", "update",
            $"Task '{entity.Title}' completed", actingUserId, ct);

        return MapToResponse(entity, links);
    }

    public async Task<TaskResponse> CancelAsync(Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await RequireTask(tenantId, id, ct);
        entity.Cancel(actingUserId);
        await _taskRepo.UpdateAsync(entity, ct);

        var links = await _taskRepo.GetLienLinksForTaskAsync(id, ct);

        _audit.Publish(
            eventType:   "liens.task.cancelled",
            action:      "update",
            description: $"Task '{entity.Title}' cancelled",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTask",
            entityId:    entity.Id.ToString());

        await PublishCaseAuditAsync(entity, links, "liens.task.cancelled", "update",
            $"Task '{entity.Title}' cancelled", actingUserId, ct);

        return MapToResponse(entity, links);
    }

    // ── LS-LIENS-FLOW-006: Start-stage derivation ─────────────────────────────
    private async Task<Guid?> DeriveStartStageAsync(
        Guid tenantId,
        LienTaskGovernanceSettings governance,
        CancellationToken ct)
    {
        if (governance.DefaultStartStageMode == StartStageMode.ExplicitStage
            && governance.ExplicitStartStageId.HasValue)
        {
            var explicit_ = await _workflowRepo.GetStageGlobalAsync(governance.ExplicitStartStageId.Value, ct);
            if (explicit_ is { IsActive: true })
                return explicit_.Id;
            // Fall through to FIRST_ACTIVE_STAGE if explicit stage is invalid
            _logger.LogWarning(
                "Governance ExplicitStartStageId {StageId} is inactive or missing; falling back to FIRST_ACTIVE_STAGE.",
                governance.ExplicitStartStageId.Value);
        }

        // FIRST_ACTIVE_STAGE — lowest StageOrder active stage
        var config = await _workflowRepo.GetByTenantProductAsync(tenantId, LiensPermissions.ProductCode, ct);
        if (config is null) return null;

        var firstStage = config.Stages
            .Where(s => s.IsActive)
            .OrderBy(s => s.StageOrder)
            .FirstOrDefault();

        return firstStage?.Id;
    }

    private async Task<LienTask> RequireTask(Guid tenantId, Guid id, CancellationToken ct)
    {
        var entity = await _taskRepo.GetByIdAsync(tenantId, id, ct);
        if (entity is null)
            throw new NotFoundException($"Task '{id}' not found.");
        return entity;
    }

    private async Task<List<LienTaskLienLink>> SaveLienLinksAsync(
        Guid taskId, IEnumerable<Guid> lienIds, Guid actingUserId, CancellationToken ct)
    {
        var links = lienIds
            .Distinct()
            .Select(lid => LienTaskLienLink.Create(taskId, lid, actingUserId))
            .ToList();

        if (links.Count > 0)
            await _taskRepo.AddLienLinksAsync(links, ct);

        return links;
    }

    private async Task PublishCaseAuditAsync(
        LienTask entity,
        List<LienTaskLienLink> links,
        string eventType,
        string action,
        string description,
        Guid actorUserId,
        CancellationToken ct)
    {
        var caseIds = new HashSet<Guid>();

        if (entity.CaseId.HasValue)
            caseIds.Add(entity.CaseId.Value);

        foreach (var link in links)
        {
            var lien = await _lienRepo.GetByIdAsync(entity.TenantId, link.LienId, ct);
            if (lien?.CaseId.HasValue == true)
                caseIds.Add(lien.CaseId.Value);
        }

        foreach (var caseId in caseIds)
        {
            _audit.Publish(
                eventType:   eventType,
                action:      action,
                description: description,
                tenantId:    entity.TenantId,
                actorUserId: actorUserId,
                entityType:  "Case",
                entityId:    caseId.ToString());
        }
    }

    private static TaskResponse MapToResponse(LienTask entity, List<LienTaskLienLink> links)
    {
        return new TaskResponse
        {
            Id                    = entity.Id,
            TenantId              = entity.TenantId,
            Title                 = entity.Title,
            Description           = entity.Description,
            Status                = entity.Status,
            Priority              = entity.Priority,
            AssignedUserId        = entity.AssignedUserId,
            CaseId                = entity.CaseId,
            WorkflowStageId       = entity.WorkflowStageId,
            DueDate               = entity.DueDate,
            CompletedAt           = entity.CompletedAt,
            ClosedByUserId        = entity.ClosedByUserId,
            CreatedByUserId       = entity.CreatedByUserId,
            CreatedAtUtc          = entity.CreatedAtUtc,
            UpdatedAtUtc          = entity.UpdatedAtUtc,
            SourceType            = entity.SourceType,
            IsSystemGenerated     = entity.SourceType == Domain.Enums.TaskSourceType.SystemGenerated,
            GenerationRuleId      = entity.GenerationRuleId,
            GeneratingTemplateId  = entity.GeneratingTemplateId,
            WorkflowInstanceId    = entity.WorkflowInstanceId,
            WorkflowStepKey       = entity.WorkflowStepKey,
            LinkedLiens           = links.Select(l => new TaskLienLinkResponse
            {
                TaskId       = l.TaskId,
                LienId       = l.LienId,
                CreatedAtUtc = l.CreatedAtUtc,
            }).ToList(),
        };
    }
}
