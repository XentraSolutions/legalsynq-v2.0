using Flow.Application.DTOs;

namespace Flow.Application.Interfaces;

/// <summary>
/// LS-FLOW-E11.5 — read-only query surface for the calling user's
/// assigned <see cref="Domain.Entities.WorkflowTask"/> rows.
///
/// <para>
/// "My tasks" is defined narrowly as
/// <c>WorkflowTask.AssignedUserId == current user id</c>. Role- and
/// org-based assignments are intentionally NOT included in this phase —
/// surfacing them would require an identity-service round-trip
/// (resolve the user's effective roles / orgs at request time) that is
/// out of scope here. Unassigned tasks are likewise excluded.
/// </para>
///
/// <para>
/// Tenant scoping is enforced automatically by the
/// <see cref="Infrastructure.Persistence.FlowDbContext"/> global query
/// filter on <c>WorkflowTask</c>; the service additionally pins the
/// query to the calling user's id pulled from
/// <see cref="Domain.Interfaces.IFlowUserContext"/>. Cross-user and
/// cross-tenant access are therefore impossible by construction.
/// </para>
/// </summary>
public interface IMyTasksService
{
    /// <summary>
    /// Returns a paginated, deterministically-ordered list of the calling
    /// user's tasks. Ordering: active tasks
    /// (<c>Open</c>, <c>InProgress</c>) first, then by
    /// <c>UpdatedAt DESC</c> (falling back to <c>CreatedAt</c> when
    /// <c>UpdatedAt</c> is null), with <c>Id</c> as a stable tiebreaker
    /// for cursor-style consistency across pages.
    /// </summary>
    Task<PagedResponse<MyTaskDto>> ListMyTasksAsync(MyTasksQuery query, CancellationToken ct = default);
}
