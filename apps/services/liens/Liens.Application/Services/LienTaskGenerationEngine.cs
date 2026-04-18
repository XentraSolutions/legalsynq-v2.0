using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienTaskGenerationEngine : ILienTaskGenerationEngine
{
    private readonly ILienTaskGenerationRuleRepository _ruleRepo;
    private readonly ILienTaskTemplateRepository       _templateRepo;
    private readonly ILienTaskRepository               _taskRepo;
    private readonly ILienTaskService                  _taskService;
    private readonly IAuditPublisher                   _audit;
    private readonly ILogger<LienTaskGenerationEngine> _logger;

    public LienTaskGenerationEngine(
        ILienTaskGenerationRuleRepository ruleRepo,
        ILienTaskTemplateRepository templateRepo,
        ILienTaskRepository taskRepo,
        ILienTaskService taskService,
        IAuditPublisher audit,
        ILogger<LienTaskGenerationEngine> logger)
    {
        _ruleRepo     = ruleRepo;
        _templateRepo = templateRepo;
        _taskRepo     = taskRepo;
        _taskService  = taskService;
        _audit        = audit;
        _logger       = logger;
    }

    public async Task<TaskGenerationResult> TriggerAsync(
        TaskGenerationContext context, CancellationToken ct = default)
    {
        if (!TaskGenerationEventType.All.Contains(context.EventType))
        {
            _logger.LogWarning("TaskGenerationEngine: unknown eventType '{EventType}'.", context.EventType);
            return new TaskGenerationResult(0, 0);
        }

        var rules = await _ruleRepo.GetActiveByTenantAndEventAsync(context.TenantId, context.EventType, ct);
        if (rules.Count == 0)
            return new TaskGenerationResult(0, 0);

        int generated = 0;
        int skipped   = 0;

        var actorUserId = context.ActorUserId ?? Guid.Empty;

        foreach (var rule in rules)
        {
            try
            {
                var outcome = await ProcessRuleAsync(rule, context, actorUserId, ct);
                if (outcome) generated++;
                else         skipped++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "TaskGenerationEngine: error processing rule {RuleId} for tenant {TenantId}.",
                    rule.Id, context.TenantId);
                skipped++;
            }
        }

        return new TaskGenerationResult(generated, skipped);
    }

    private async Task<bool> ProcessRuleAsync(
        LienTaskGenerationRule rule,
        TaskGenerationContext context,
        Guid actorUserId,
        CancellationToken ct)
    {
        // 1. Stage filter
        if (rule.ApplicableWorkflowStageId.HasValue
            && rule.ApplicableWorkflowStageId.Value != context.WorkflowStageId)
        {
            _logger.LogDebug(
                "Rule {RuleId}: stage mismatch (rule requires {Required}, context has {Actual}). Skipping.",
                rule.Id, rule.ApplicableWorkflowStageId, context.WorkflowStageId);
            return false;
        }

        // 2. Template check
        var template = await _templateRepo.GetByIdAsync(context.TenantId, rule.TaskTemplateId, ct);
        if (template is null || !template.IsActive)
        {
            _logger.LogWarning(
                "Rule {RuleId}: template {TemplateId} not found or inactive. Skipping.",
                rule.Id, rule.TaskTemplateId);
            _audit.Publish(
                eventType:   "liens.task.auto_generation_skipped",
                action:      "auto_generate_skipped",
                description: $"Skipped: template {rule.TaskTemplateId} not found or inactive for rule '{rule.Name}'",
                tenantId:    context.TenantId,
                actorUserId: actorUserId,
                entityType:  "LienTaskGenerationRule",
                entityId:    rule.Id.ToString());
            return false;
        }

        // 3. Duplicate prevention
        var dupMode = rule.DuplicatePreventionMode;
        if (dupMode == DuplicatePreventionMode.SameRuleSameEntityOpenTask)
        {
            var hasDup = await _taskRepo.HasOpenTaskForRuleAsync(
                context.TenantId, rule.Id, context.CaseId, context.LienId, ct);
            if (hasDup)
            {
                _logger.LogInformation(
                    "Rule {RuleId}: duplicate found (SAME_RULE). Skipping.", rule.Id);
                _audit.Publish(
                    eventType:   "liens.task.auto_generation_skipped",
                    action:      "auto_generate_skipped",
                    description: $"Skipped: open task already exists for rule '{rule.Name}' (SAME_RULE_SAME_ENTITY_OPEN_TASK)",
                    tenantId:    context.TenantId,
                    actorUserId: actorUserId,
                    entityType:  "LienTaskGenerationRule",
                    entityId:    rule.Id.ToString());
                return false;
            }
        }
        else if (dupMode == DuplicatePreventionMode.SameTemplateSameEntityOpenTask)
        {
            var hasDup = await _taskRepo.HasOpenTaskForTemplateAsync(
                context.TenantId, rule.TaskTemplateId, context.CaseId, context.LienId, ct);
            if (hasDup)
            {
                _logger.LogInformation(
                    "Rule {RuleId}: duplicate found (SAME_TEMPLATE). Skipping.", rule.Id);
                _audit.Publish(
                    eventType:   "liens.task.auto_generation_skipped",
                    action:      "auto_generate_skipped",
                    description: $"Skipped: open task already exists for template '{template.Name}' (SAME_TEMPLATE_SAME_ENTITY_OPEN_TASK)",
                    tenantId:    context.TenantId,
                    actorUserId: actorUserId,
                    entityType:  "LienTaskGenerationRule",
                    entityId:    rule.Id.ToString());
                return false;
            }
        }

        // 4. Build task request
        var assignedUserId = ResolveAssignee(rule.AssignmentMode, template.DefaultRoleId, actorUserId, context);
        var dueDate        = ResolveDueDate(rule.DueDateMode, template.DefaultDueOffsetDays, rule.DueDateOffsetDays);

        var lienIds = context.LienId.HasValue
            ? new List<Guid> { context.LienId.Value }
            : new List<Guid>();

        var createRequest = new CreateTaskRequest
        {
            Title                 = template.DefaultTitle,
            Description           = template.DefaultDescription,
            Priority              = template.DefaultPriority,
            AssignedUserId        = assignedUserId,
            CaseId                = context.CaseId,
            LienIds               = lienIds,
            WorkflowStageId       = context.WorkflowStageId ?? template.ApplicableWorkflowStageId,
            DueDate               = dueDate,
            SourceType            = TaskSourceType.SystemGenerated,
            GenerationRuleId      = rule.Id,
            GeneratingTemplateId  = rule.TaskTemplateId,
        };

        // actorUserId guard — system-generated tasks use the event actor if available,
        // else we cannot use Guid.Empty as createdByUserId. Fall back to a deterministic
        // placeholder (we never call with Guid.Empty as that throws in LienTask.Create).
        if (actorUserId == Guid.Empty)
        {
            _logger.LogWarning("Rule {RuleId}: no actor userId available; task generation requires a valid userId. Skipping.", rule.Id);
            return false;
        }

        // 5. Create task via normal pipeline
        var taskResponse = await _taskService.CreateAsync(context.TenantId, actorUserId, createRequest, ct);

        // 6. Save traceability metadata
        var metadata = LienGeneratedTaskMetadata.Create(
            taskId:            taskResponse.Id,
            tenantId:          context.TenantId,
            generationRuleId:  rule.Id,
            taskTemplateId:    rule.TaskTemplateId,
            triggerEventType:  context.EventType,
            triggerEntityType: context.EntityType,
            triggerEntityId:   context.EntityId.ToString());

        await _taskRepo.AddGeneratedMetadataAsync(metadata, ct);

        // 7. Audit success
        _audit.Publish(
            eventType:   "liens.task.auto_generated",
            action:      "auto_generate",
            description: $"Task '{taskResponse.Title}' auto-generated by rule '{rule.Name}' on event {context.EventType}",
            tenantId:    context.TenantId,
            actorUserId: actorUserId,
            entityType:  "LienTask",
            entityId:    taskResponse.Id.ToString(),
            metadata:    $"{{\"ruleId\":\"{rule.Id}\",\"templateId\":\"{rule.TaskTemplateId}\",\"eventType\":\"{context.EventType}\",\"triggerEntityId\":\"{context.EntityId}\"}}");

        _logger.LogInformation(
            "TaskGenerationEngine: task {TaskId} generated for rule {RuleId} event {EventType}.",
            taskResponse.Id, rule.Id, context.EventType);

        return true;
    }

    private static Guid? ResolveAssignee(
        string assignmentMode,
        string? templateRoleId,
        Guid actorUserId,
        TaskGenerationContext context)
    {
        return assignmentMode switch
        {
            AssignmentMode.LeaveUnassigned  => null,
            AssignmentMode.AssignEventActor => actorUserId == Guid.Empty ? null : actorUserId,
            AssignmentMode.AssignByRole     => null, // Deferred: no role-to-user resolution available
            _                               => null, // UseTemplateDefault: template has no assignedUserId directly
        };
    }

    private static DateTime? ResolveDueDate(
        string dueDateMode,
        int? templateOffsetDays,
        int? ruleOffsetDays)
    {
        return dueDateMode switch
        {
            DueDateMode.NoDueDate         => null,
            DueDateMode.OverrideOffsetDays => ruleOffsetDays.HasValue
                ? DateTime.UtcNow.AddDays(ruleOffsetDays.Value)
                : null,
            _ => templateOffsetDays.HasValue
                ? DateTime.UtcNow.AddDays(templateOffsetDays.Value)
                : null,
        };
    }
}
