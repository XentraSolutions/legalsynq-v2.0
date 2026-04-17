using Flow.Application.Engines.WorkflowEngine;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E11.2 — production implementation of
/// <see cref="IWorkflowTaskFromWorkflowFactory"/>. See the interface
/// docs and <c>analysis/E11.2-report.md</c> for the rule set.
///
/// <para>
/// Transactional model: the factory only <b>stages</b> a new
/// <see cref="WorkflowTask"/> on the shared <see cref="IFlowDbContext"/>.
/// The caller's <c>SaveChangesAsync</c> commits both the workflow state
/// change and the task in a single EF unit-of-work, so a workflow
/// transition that fails to persist cannot leave behind an orphan task.
/// </para>
///
/// <para>
/// Duplicate prevention is enforced at the application layer in two
/// passes:
///   <list type="number">
///     <item>An in-memory check against the EF change-tracker
///       (<c>DbSet.Local</c>) so a single unit-of-work cannot accidentally
///       queue two tasks for the same step.</item>
///     <item>A no-tracking <c>AnyAsync</c> against the DB for an
///       existing Open / InProgress task at <c>(instanceId, stepKey)</c>.</item>
///   </list>
/// Both checks are optimistic — see report §"Duplicate Prevention Notes"
/// for the documented narrow race window between two concurrent admin
/// retries on the same instance.
/// </para>
/// </summary>
public sealed class WorkflowTaskFromWorkflowFactory : IWorkflowTaskFromWorkflowFactory
{
    /// <summary>
    /// Title cap kept symmetrical with the EF column max length on
    /// <see cref="WorkflowTask.Title"/> (512). A trim here keeps a long
    /// definition name from triggering an "Data too long for column"
    /// MySQL failure at SaveChanges.
    /// </summary>
    private const int MaxTitleLength = 512;

    private readonly IFlowDbContext _db;
    private readonly ILogger<WorkflowTaskFromWorkflowFactory> _logger;

    public WorkflowTaskFromWorkflowFactory(
        IFlowDbContext db,
        ILogger<WorkflowTaskFromWorkflowFactory> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<WorkflowTask?> EnsureForCurrentStepAsync(
        WorkflowInstance instance,
        CancellationToken cancellationToken = default)
    {
        if (instance is null) throw new ArgumentNullException(nameof(instance));

        // ── Eligibility ────────────────────────────────────────────────
        // Only Active instances participate. Completed / Cancelled /
        // Failed instances must never produce new work items — they
        // are by definition no longer driving human work. This is the
        // single rule that makes the integration "safe by default":
        // calling EnsureForCurrentStepAsync after any transition is a
        // no-op when the new state is terminal.
        if (!string.Equals(instance.Status, WorkflowEngine.StatusActive, StringComparison.Ordinal))
        {
            return null;
        }

        // No step → nothing to raise a task against. Defensive: today
        // the engine always assigns CurrentStepKey when entering Active,
        // but a definition with no stages can leave it null.
        var stepKey = instance.CurrentStepKey;
        if (string.IsNullOrWhiteSpace(stepKey))
        {
            return null;
        }

        var instanceId = instance.Id;

        // ── Dedup #1: in-flight Added rows in the same DbContext ──────
        // Catches the (rare) case where the same caller already queued
        // a task for this step in the current unit of work. Avoids two
        // INSERTs from the same SaveChanges batch.
        var pending = _db.WorkflowTasks.Local
            .FirstOrDefault(t => t.WorkflowInstanceId == instanceId
                              && string.Equals(t.StepKey, stepKey, StringComparison.Ordinal)
                              && (t.Status == WorkflowTaskStatus.Open
                                || t.Status == WorkflowTaskStatus.InProgress));
        if (pending is not null)
        {
            return null;
        }

        // ── Dedup #2: committed Open / InProgress task at this step ──
        // Re-entry semantics: if every prior task at this step is
        // terminal (Completed / Cancelled), this query returns false
        // and a fresh task is created — that is the documented
        // re-entry behaviour.
        //
        // SECURITY / CORRECTNESS — IgnoreQueryFilters() + explicit
        // TenantId predicate. The DbContext applies a tenant query
        // filter (`e.TenantId == _tenantProvider.GetTenantId()`) by
        // default, but platform-admin retry paths load the parent
        // instance via IgnoreQueryFilters() and may execute against an
        // instance whose TenantId does NOT match the ambient tenant
        // claim (or with no tenant claim at all). Without disabling the
        // filter here the dedup query would silently miss existing
        // rows and we would insert a duplicate task — or throw on a
        // null tenant provider mid-request. Pinning to
        // instance.TenantId is the authoritative tenant for this work.
        var tenantId = instance.TenantId;
        var hasActive = await _db.WorkflowTasks
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(t => t.TenantId == tenantId
                        && t.WorkflowInstanceId == instanceId
                        && t.StepKey == stepKey
                        && (t.Status == WorkflowTaskStatus.Open
                          || t.Status == WorkflowTaskStatus.InProgress),
                      cancellationToken);
        if (hasActive)
        {
            return null;
        }

        // ── Title ─────────────────────────────────────────────────────
        // Deterministic, definition-driven, no UI-only fields. Falls
        // back to ProductKey when the definition has no display name
        // (legacy / minimally-seeded definitions).
        //
        // Same IgnoreQueryFilters() + explicit TenantId rationale as
        // the dedup query above — a cross-tenant admin retry would
        // otherwise silently drop the definition lookup and degrade
        // every task title to the ProductKey fallback.
        var workflowName = await _db.FlowDefinitions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.Id == instance.WorkflowDefinitionId
                     && d.TenantId == tenantId)
            .Select(d => d.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var prefix = !string.IsNullOrWhiteSpace(workflowName)
            ? workflowName!
            : instance.ProductKey;

        var title = $"{prefix} — {stepKey}";
        if (title.Length > MaxTitleLength) title = title[..MaxTitleLength];

        // ── Stage the task ────────────────────────────────────────────
        // TenantId is set explicitly from the parent instance so the
        // value is correct regardless of the calling context (engine,
        // controller, background service). The DbContext save-hook
        // would also stamp it from the ambient ITenantProvider, but
        // copying from the instance is unambiguous and survives any
        // future caller that runs outside a tenant-resolved request
        // scope.
        var task = new WorkflowTask
        {
            TenantId           = instance.TenantId,
            WorkflowInstanceId = instanceId,
            StepKey            = stepKey,
            Title              = title,
            Status             = WorkflowTaskStatus.Open,
            Priority           = WorkflowTaskPriority.Normal,
        };

        _db.WorkflowTasks.Add(task);

        _logger.LogInformation(
            "WorkflowTaskFromWorkflowFactory.Ensure created task instance={InstanceId} tenant={TenantId} step={StepKey} title={Title}",
            instanceId, instance.TenantId, stepKey, title);

        return task;
    }
}
