using BuildingBlocks.Authorization;
using Flow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Controllers.V1;

/// <summary>
/// LS-FLOW-E11.5 — HTTP surface for the
/// <see cref="IWorkflowTaskLifecycleService"/> shipped in E11.4.
/// Owns the three lifecycle transitions on a single
/// <see cref="Domain.Entities.WorkflowTask"/>:
/// <c>start</c> (Open → InProgress), <c>complete</c>
/// (InProgress → Completed), and <c>cancel</c>
/// (Open|InProgress → Cancelled).
///
/// <para>
/// <b>Why a separate controller from <c>MyTasksController</c>?</b>
/// "My Tasks" is a per-user query surface; lifecycle is a per-task
/// mutation surface. Keeping them apart lets each be authorised,
/// tested, and rate-limited independently, and avoids the implication
/// that lifecycle is restricted to tasks visible in <c>/me</c> (it is
/// not — any tenant-visible task may be transitioned by a permitted
/// caller).
/// </para>
///
/// <para>
/// <b>Tenant safety:</b> enforced by the <c>WorkflowTask</c> global
/// query filter inside the service; cross-tenant calls surface as
/// <see cref="Application.Exceptions.NotFoundException"/> ⇒ <c>404</c>
/// (identical to a missing task, by design).
/// </para>
///
/// <para>
/// <b>Error mapping</b> (handled by <c>ExceptionHandlingMiddleware</c>,
/// already wired in E11.4):
/// </para>
/// <list type="bullet">
///   <item><c>NotFoundException</c> ⇒ <c>404</c> — task missing or in a different tenant.</item>
///   <item><c>InvalidStateTransitionException</c> ⇒ <c>422</c> — caller asked for a transition the current state forbids (e.g. <c>Complete</c> on an <c>Open</c> task). Semantic, not concurrency.</item>
///   <item><c>WorkflowTaskConcurrencyException</c> ⇒ <c>409</c> — CAS lost a race; safe to re-read and retry.</item>
///   <item><c>ValidationException</c> ⇒ <c>400</c> — malformed input (reserved; lifecycle methods take only an id).</item>
/// </list>
///
/// <para>
/// All endpoints return <see cref="WorkflowTaskTransitionResult"/> so
/// the caller can update its UI without a follow-up GET.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/workflow-tasks")]
[Authorize(Policy = Policies.AuthenticatedUser)]
public sealed class WorkflowTasksController : ControllerBase
{
    private readonly IWorkflowTaskLifecycleService _lifecycle;
    private readonly IWorkflowTaskCompletionService _completion;

    public WorkflowTasksController(
        IWorkflowTaskLifecycleService lifecycle,
        IWorkflowTaskCompletionService completion)
    {
        _lifecycle = lifecycle;
        _completion = completion;
    }

    /// <summary>POST <c>/api/v1/workflow-tasks/{id}/start</c> — Open → InProgress.</summary>
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(WorkflowTaskTransitionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        var result = await _lifecycle.StartTaskAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// POST <c>/api/v1/workflow-tasks/{id}/complete</c> — atomic
    /// "complete the task AND advance the owning workflow".
    ///
    /// <para>
    /// LS-FLOW-E11.7 wired this endpoint through
    /// <see cref="IWorkflowTaskCompletionService"/> so completing the
    /// correct active task progresses its workflow through the engine
    /// in the same transaction. Stale-step / non-active-workflow
    /// failures roll the task transition back; duplicate completions
    /// are blocked by the lifecycle CAS. The external request shape is
    /// unchanged from E11.5.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(WorkflowTaskCompletionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        var result = await _completion.CompleteAndProgressAsync(id, ct);
        return Ok(result);
    }

    /// <summary>POST <c>/api/v1/workflow-tasks/{id}/cancel</c> — (Open|InProgress) → Cancelled.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(WorkflowTaskTransitionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _lifecycle.CancelTaskAsync(id, ct);
        return Ok(result);
    }
}
