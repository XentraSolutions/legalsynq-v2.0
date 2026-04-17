namespace Flow.Application.DTOs;

/// <summary>
/// LS-FLOW-E11.5 — UI-friendly projection of a
/// <see cref="Domain.Entities.WorkflowTask"/> for the current
/// authenticated user's "My Tasks" surface.
///
/// <para>
/// Deliberately narrow: only fields the operator portal needs to render
/// a task row + drawer. Internal engine fields
/// (<c>MetadataJson</c>, <c>CorrelationKey</c>, role/org assignments,
/// audit columns beyond the four lifecycle timestamps) are intentionally
/// excluded so the contract does not couple the UI to engine internals
/// and so future engine changes can stay backward-compatible.
/// </para>
///
/// <para>
/// All values originate from <c>WorkflowTask</c> directly except the two
/// optional workflow-context fields, which are joined from the owning
/// <c>WorkflowInstance</c> / <c>FlowDefinition</c> in the SAME query
/// (no N+1).
/// </para>
/// </summary>
public sealed record MyTaskDto
{
    public Guid TaskId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string StepKey { get; init; } = string.Empty;

    /// <summary>The current user's id — always set on rows returned by this surface.</summary>
    public string? AssignedUserId { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CancelledAt { get; init; }

    // ---------------- Minimal workflow context -----------------
    /// <summary>Owning workflow instance — opaque to the UI, useful as a deep-link target.</summary>
    public Guid WorkflowInstanceId { get; init; }
    /// <summary>Optional human-readable workflow name (FlowDefinition.Name). Null if join missed.</summary>
    public string? WorkflowName { get; init; }
    /// <summary>Optional product key the instance belongs to. Null if join missed.</summary>
    public string? ProductKey { get; init; }
}

/// <summary>
/// LS-FLOW-E11.5 — server-side query parameters for the My Tasks
/// endpoint. Populated from the controller's query string. Intentionally
/// narrow: no arbitrary user / role / org / context filters — the surface
/// is hard-scoped to the calling user.
/// </summary>
public sealed record MyTasksQuery
{
    /// <summary>Optional status filter; multiple values may be passed (<c>?status=Open&amp;status=InProgress</c>).</summary>
    public IReadOnlyList<string>? Status { get; init; }

    /// <summary>1-based page index. Defaults to 1; values &lt; 1 are normalised to 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Page size. Defaults to 25; clamped to <see cref="MyTasksDefaults.MaxPageSize"/>.</summary>
    public int PageSize { get; init; } = MyTasksDefaults.DefaultPageSize;
}

/// <summary>
/// LS-FLOW-E11.5 — pagination + safety constants for the My Tasks
/// endpoint. Centralised so the controller, service, and report can
/// reference identical values.
/// </summary>
public static class MyTasksDefaults
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
}
