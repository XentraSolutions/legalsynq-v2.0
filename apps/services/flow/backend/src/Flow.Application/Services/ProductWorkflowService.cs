using Flow.Application.DTOs;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-MERGE-P3 — owns the explicit product↔workflow correlation.
/// Creates a <see cref="ProductWorkflowMapping"/> row plus the initial
/// Flow <see cref="TaskItem"/> (the workflow instance grain today), under
/// the existing tenant + product invariants.
/// </summary>
public sealed class ProductWorkflowService : IProductWorkflowService
{
    private readonly IFlowDbContext _db;
    private readonly ITaskService _taskService;

    public ProductWorkflowService(IFlowDbContext db, ITaskService taskService)
    {
        _db = db;
        _taskService = taskService;
    }

    public async Task<ProductWorkflowResponse> CreateAsync(string productKey, CreateProductWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(productKey, request);

        var workflow = await _db.FlowDefinitions
            .AsNoTracking()
            .Where(w => w.Id == request.WorkflowDefinitionId)
            .Select(w => new { w.Id, w.ProductKey })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ValidationException($"Workflow {request.WorkflowDefinitionId} not found.");

        if (!string.Equals(workflow.ProductKey, productKey, StringComparison.Ordinal))
        {
            throw new ValidationException(
                $"Workflow {workflow.Id} belongs to product {workflow.ProductKey}, not {productKey}.");
        }

        // LS-FLOW-MERGE-P3/P4 — task + workflow-instance + mapping must be
        // one atomic unit so a partial failure cannot orphan a Flow
        // TaskItem or leave a mapping pointing nowhere. EF's execution
        // strategy owns retry; the explicit transaction lives inside it.
        ProductWorkflowMapping? mapping = null;
        var strategy = _db.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            await using var tx = await _db.BeginTransactionAsync(ct);

            var task = await _taskService.CreateAsync(new CreateTaskRequest
            {
                Title = request.Title,
                Description = request.Description,
                ProductKey = productKey,
                FlowDefinitionId = request.WorkflowDefinitionId,
                AssignedToUserId = request.AssignedToUserId,
                AssignedToRoleKey = request.AssignedToRoleKey,
                AssignedToOrgId = request.AssignedToOrgId,
                DueDate = request.DueDate,
                Context = new ContextReferenceDto
                {
                    ContextType = request.SourceEntityType.Trim(),
                    ContextId = request.SourceEntityId.Trim(),
                    Label = request.CorrelationKey
                }
            }, ct);

            // LS-FLOW-MERGE-P4 — create the canonical WorkflowInstance row
            // alongside the bootstrapping task.
            var instance = new WorkflowInstance
            {
                WorkflowDefinitionId = request.WorkflowDefinitionId,
                ProductKey = productKey,
                CorrelationKey = string.IsNullOrWhiteSpace(request.CorrelationKey) ? null : request.CorrelationKey.Trim(),
                InitialTaskId = task.Id,
                Status = "Active"
            };
            _db.WorkflowInstances.Add(instance);
            await _db.SaveChangesAsync(ct);

            mapping = new ProductWorkflowMapping
            {
                ProductKey = productKey,
                SourceEntityType = request.SourceEntityType.Trim(),
                SourceEntityId = request.SourceEntityId.Trim(),
                WorkflowDefinitionId = request.WorkflowDefinitionId,
                WorkflowInstanceId = instance.Id,
                WorkflowInstanceTaskId = task.Id, // legacy back-compat
                CorrelationKey = instance.CorrelationKey,
                Status = "Active"
            };

            _db.ProductWorkflowMappings.Add(mapping);
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
        }, cancellationToken);

        return Map(mapping!);
    }

    public async Task<List<ProductWorkflowResponse>> ListByProductEntityAsync(string productKey, string sourceEntityType, string sourceEntityId, CancellationToken cancellationToken = default)
    {
        EnsureValidProductKey(productKey);

        if (string.IsNullOrWhiteSpace(sourceEntityType) || string.IsNullOrWhiteSpace(sourceEntityId))
            throw new ValidationException("sourceEntityType and sourceEntityId are required.");

        var rows = await _db.ProductWorkflowMappings
            .AsNoTracking()
            .Where(m => m.ProductKey == productKey
                && m.SourceEntityType == sourceEntityType.Trim()
                && m.SourceEntityId == sourceEntityId.Trim())
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<List<ProductWorkflowResponse>> ListByProductAsync(string productKey, CancellationToken cancellationToken = default)
    {
        EnsureValidProductKey(productKey);

        var rows = await _db.ProductWorkflowMappings
            .AsNoTracking()
            .Where(m => m.ProductKey == productKey)
            .OrderByDescending(m => m.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<ProductWorkflowResponse> GetByIdAsync(string productKey, Guid id, CancellationToken cancellationToken = default)
    {
        EnsureValidProductKey(productKey);

        var row = await _db.ProductWorkflowMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.ProductKey == productKey, cancellationToken)
            ?? throw new NotFoundException("ProductWorkflowMapping", id);

        return Map(row);
    }

    private static void ValidateRequest(string productKey, CreateProductWorkflowRequest request)
    {
        EnsureValidProductKey(productKey);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.SourceEntityType))
            errors.Add("sourceEntityType is required.");
        if (string.IsNullOrWhiteSpace(request.SourceEntityId))
            errors.Add("sourceEntityId is required.");
        if (request.WorkflowDefinitionId == Guid.Empty)
            errors.Add("workflowDefinitionId is required.");
        if (string.IsNullOrWhiteSpace(request.Title))
            errors.Add("title is required.");
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private static void EnsureValidProductKey(string productKey)
    {
        if (!ProductKeys.IsValid(productKey))
            throw new ValidationException($"Unsupported productKey: {productKey}");
        if (productKey == ProductKeys.FlowGeneric)
            throw new ValidationException("Product-facing endpoints require a real product key (FLOW_GENERIC is platform-only).");
    }

    private static ProductWorkflowResponse Map(ProductWorkflowMapping m) => new()
    {
        Id = m.Id,
        ProductKey = m.ProductKey,
        SourceEntityType = m.SourceEntityType,
        SourceEntityId = m.SourceEntityId,
        WorkflowDefinitionId = m.WorkflowDefinitionId,
        WorkflowInstanceId = m.WorkflowInstanceId,
        WorkflowInstanceTaskId = m.WorkflowInstanceTaskId,
        CorrelationKey = m.CorrelationKey,
        Status = m.Status,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
