namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-MERGE-P4 — request to start a product-correlated workflow via Flow.
/// Mirrors <c>Flow.Application.DTOs.CreateProductWorkflowRequest</c> on the
/// wire; the shared client owns its own type to keep BuildingBlocks free of
/// a dependency on Flow.Application.
/// </summary>
public sealed class StartProductWorkflowRequest
{
    public string SourceEntityType { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CorrelationKey { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToRoleKey { get; set; }
    public string? AssignedToOrgId { get; set; }
    public DateTime? DueDate { get; set; }
}

/// <summary>
/// LS-FLOW-MERGE-P4 — response shape returned by Flow's product-workflow
/// endpoints. Mirrors <c>Flow.Application.DTOs.ProductWorkflowResponse</c>.
/// </summary>
public sealed class FlowProductWorkflowResponse
{
    public Guid Id { get; set; }
    public string ProductKey { get; set; } = string.Empty;
    public string SourceEntityType { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? WorkflowInstanceTaskId { get; set; }
    public string? CorrelationKey { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
