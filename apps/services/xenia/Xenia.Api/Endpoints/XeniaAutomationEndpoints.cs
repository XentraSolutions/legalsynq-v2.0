using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Xenia.Application.Automation;
using Xenia.Application.Automation.Models;
using Xenia.Domain.Automation;

namespace Xenia.Api.Endpoints;

/// <summary>
/// Automation registry, discovery, execution, dead-letter, and diagnostics endpoints.
///
/// Authorization: platform-admin only for registry management;
///                module-level permission for execution.
///
/// Phase 1: basic CRUD + execution. Scheduling UI is Phase H.
/// </summary>
public static class XeniaAutomationEndpoints
{
    public static IEndpointRouteBuilder MapAutomationEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/v1/automation").WithTags("Automation");

        // ── Discovery ──────────────────────────────────────────────────
        grp.MapGet("/", async (
            [FromQuery] Guid? tenantId,
            IAutomationDiscoveryService discovery,
            CancellationToken ct) =>
        {
            var manifests = await discovery.DiscoverAllAsync(tenantId, ct);
            return Results.Ok(new { Items = manifests, TotalCount = manifests.Count });
        })
        .WithName("GetAutomations")
        .WithSummary("List all registered automations");

        grp.MapGet("/{automationKey}", async (
            string automationKey,
            IAutomationDiscoveryService discovery,
            CancellationToken ct) =>
        {
            var manifest = await discovery.DiscoverByKeyAsync(automationKey, ct);
            return manifest is null ? Results.NotFound() : Results.Ok(manifest);
        })
        .WithName("GetAutomation")
        .WithSummary("Get automation manifest by key");

        // ── Lifecycle management ───────────────────────────────────────
        grp.MapPost("/{automationKey}/enable", async (
            string automationKey,
            [FromQuery] Guid? tenantId,
            [FromQuery] Guid actorId,
            IAutomationRegistry registry,
            CancellationToken ct) =>
        {
            bool ok = tenantId.HasValue
                ? await registry.EnableForTenantAsync(automationKey, tenantId.Value, actorId, ct)
                : await registry.EnableGloballyAsync(automationKey, actorId, ct);
            return ok ? Results.Ok(new { Enabled = true }) : Results.NotFound();
        })
        .WithName("EnableAutomation")
        .WithSummary("Enable an automation globally or for a tenant");

        grp.MapPost("/{automationKey}/disable", async (
            string automationKey,
            [FromQuery] Guid? tenantId,
            [FromQuery] Guid actorId,
            IAutomationRegistry registry,
            CancellationToken ct) =>
        {
            bool ok = tenantId.HasValue
                ? await registry.DisableForTenantAsync(automationKey, tenantId.Value, actorId, ct)
                : await registry.DisableGloballyAsync(automationKey, actorId, ct);
            return ok ? Results.Ok(new { Disabled = true }) : Results.NotFound();
        })
        .WithName("DisableAutomation")
        .WithSummary("Disable an automation globally or for a tenant");

        grp.MapGet("/{automationKey}/state", async (
            string automationKey,
            [FromQuery] Guid? tenantId,
            IAutomationRegistry registry,
            CancellationToken ct) =>
        {
            var state = await registry.GetRuntimeStateAsync(automationKey, tenantId, ct);
            if (state is null) return Results.NotFound();
            return Results.Ok(new
            {
                state.AutomationKey,
                state.AutomationVersion,
                state.TenantId,
                GlobalState      = state.GlobalState.ToString(),
                TenantState      = state.TenantState?.ToString(),
                EffectiveState   = state.EffectiveState.ToString(),
                state.ActiveExecutions,
                state.TotalExecutions,
                state.FailedExecutions,
                state.LastExecutedAt,
                state.LastSucceededAt,
                state.LastSafeError,
            });
        })
        .WithName("GetAutomationState")
        .WithSummary("Get runtime state for an automation");

        grp.MapGet("/{automationKey}/dependencies", async (
            string automationKey,
            IAutomationRegistry registry,
            CancellationToken ct) =>
        {
            var deps = await registry.GetDependenciesAsync(automationKey, ct);
            return Results.Ok(new { Items = deps, TotalCount = deps.Count });
        })
        .WithName("GetAutomationDependencies")
        .WithSummary("Get declared dependencies for an automation");

        // ── Execution ─────────────────────────────────────────────────
        grp.MapPost("/{automationKey}/execute", async (
            string automationKey,
            [FromBody] ExecuteAutomationRequest body,
            IAutomationExecutionService executor,
            CancellationToken ct) =>
        {
            var request = new AutomationExecutionRequest
            {
                AutomationKey    = automationKey,
                AutomationVersion = body.Version,
                Context          = new AutomationContext
                {
                    TenantId      = body.TenantId,
                    ActorId       = body.ActorId,
                    CorrelationId = body.CorrelationId,
                    Metadata      = body.Metadata ?? new Dictionary<string, string>(),
                },
                TriggerType      = AutomationTriggerType.Manual,
                IdempotencyKey   = body.IdempotencyKey ?? Guid.CreateVersion7().ToString(),
                Parameters       = body.Parameters ?? new Dictionary<string, string>(),
            };
            var result = await executor.ExecuteAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.UnprocessableEntity(result);
        })
        .WithName("ExecuteAutomation")
        .WithSummary("Execute an automation manually");

        grp.MapPost("/{automationKey}/executions/{executionId}/cancel", async (
            string automationKey,
            Guid executionId,
            [FromQuery] Guid? tenantId,
            IAutomationExecutionService executor,
            CancellationToken ct) =>
        {
            var ok = await executor.CancelAsync(automationKey, executionId, tenantId, ct);
            return ok ? Results.Ok(new { Cancelled = true }) : Results.NotFound();
        })
        .WithName("CancelAutomationExecution")
        .WithSummary("Cancel a running automation execution");

        grp.MapGet("/{automationKey}/executions", async (
            string automationKey,
            [FromQuery] Guid? tenantId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            IAutomationExecutionService executor = null!,
            CancellationToken ct = default) =>
        {
            var history = await executor.GetExecutionHistoryAsync(automationKey, tenantId, page, pageSize, ct);
            return Results.Ok(new { Items = history, Page = page, PageSize = pageSize });
        })
        .WithName("GetAutomationExecutionHistory")
        .WithSummary("Get execution history for an automation");

        grp.MapGet("/executions/{executionId}", async (
            Guid executionId,
            [FromQuery] Guid? tenantId,
            IAutomationExecutionService executor,
            CancellationToken ct) =>
        {
            var meta = await executor.GetExecutionAsync(executionId, tenantId, ct);
            return meta is null ? Results.NotFound() : Results.Ok(meta);
        })
        .WithName("GetAutomationExecution")
        .WithSummary("Get a specific automation execution");

        // ── Dead-letter ────────────────────────────────────────────────
        var dlq = app.MapGroup("/api/v1/automation-dlq").WithTags("Automation");

        dlq.MapGet("/", async (
            [FromQuery] string? automationKey,
            [FromQuery] Guid? tenantId,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            IAutomationDeadLetterStore store = null!,
            CancellationToken ct = default) =>
        {
            AutomationDeadLetterStatus? parsedStatus = null;
            if (status is not null && Enum.TryParse<AutomationDeadLetterStatus>(status, true, out var s))
                parsedStatus = s;
            var items = await store.ListAsync(automationKey, tenantId, parsedStatus, page, pageSize, ct);
            return Results.Ok(new { Items = items, Page = page, PageSize = pageSize });
        })
        .WithName("ListDeadLetterEntries")
        .WithSummary("List automation dead-letter entries");

        dlq.MapPost("/{id}/retry", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            IAutomationDeadLetterStore store,
            CancellationToken ct) =>
        {
            var ok = await store.RetryAsync(id, tenantId, DateTime.UtcNow.AddMinutes(5), ct);
            return ok ? Results.Ok(new { Retrying = true }) : Results.NotFound();
        })
        .WithName("RetryDeadLetterEntry")
        .WithSummary("Mark a dead-letter entry for retry");

        dlq.MapPost("/{id}/abandon", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            IAutomationDeadLetterStore store,
            CancellationToken ct) =>
        {
            var ok = await store.AbandonAsync(id, tenantId, ct);
            return ok ? Results.Ok(new { Abandoned = true }) : Results.NotFound();
        })
        .WithName("AbandonDeadLetterEntry")
        .WithSummary("Abandon a dead-letter entry");

        dlq.MapPost("/{id}/resolve", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            IAutomationDeadLetterStore store,
            CancellationToken ct) =>
        {
            var ok = await store.ResolveAsync(id, tenantId, ct);
            return ok ? Results.Ok(new { Resolved = true }) : Results.NotFound();
        })
        .WithName("ResolveDeadLetterEntry")
        .WithSummary("Mark a dead-letter entry as resolved");

        // ── Diagnostics ────────────────────────────────────────────────
        var diag = app.MapGroup("/api/v1/automation-diagnostics").WithTags("Automation");

        diag.MapGet("/snapshot", async (
            [FromQuery] Guid? tenantId,
            IAutomationDiagnosticsService diagnostics,
            CancellationToken ct) =>
            Results.Ok(await diagnostics.GetSnapshotAsync(tenantId, ct)))
        .WithName("GetAutomationDiagnosticsSnapshot")
        .WithSummary("Get diagnostics snapshot for all registered automations");

        diag.MapGet("/support-bundle", async (
            [FromQuery] Guid? tenantId,
            IAutomationDiagnosticsService diagnostics,
            CancellationToken ct) =>
            Results.Ok(await diagnostics.GenerateSupportBundleAsync(tenantId, ct)))
        .WithName("GetAutomationSupportBundle")
        .WithSummary("Generate a safe support bundle for automation diagnostics");

        // ── Scheduling ─────────────────────────────────────────────────
        var sched = app.MapGroup("/api/v1/automation-schedules").WithTags("Automation");

        sched.MapGet("/", async (
            [FromQuery] Guid? tenantId,
            IAutomationScheduler scheduler,
            CancellationToken ct) =>
        {
            var schedules = await scheduler.GetAllSchedulesAsync(tenantId, ct);
            return Results.Ok(new { Items = schedules, SchedulingEnabled = scheduler.IsSchedulingEnabled });
        })
        .WithName("GetAutomationSchedules")
        .WithSummary("List all automation schedules");

        sched.MapGet("/{automationKey}", async (
            string automationKey,
            [FromQuery] Guid? tenantId,
            IAutomationScheduler scheduler,
            CancellationToken ct) =>
        {
            var schedule = await scheduler.GetScheduleAsync(automationKey, tenantId, ct);
            return schedule is null ? Results.NotFound() : Results.Ok(schedule);
        })
        .WithName("GetAutomationSchedule")
        .WithSummary("Get schedule for an automation");

        sched.MapDelete("/{automationKey}", async (
            string automationKey,
            [FromQuery] Guid? tenantId,
            IAutomationScheduler scheduler,
            CancellationToken ct) =>
        {
            var ok = await scheduler.DisableScheduleAsync(automationKey, tenantId, ct);
            return ok ? Results.Ok(new { Disabled = true }) : Results.NotFound();
        })
        .WithName("DisableAutomationSchedule")
        .WithSummary("Disable schedule for an automation");

        return app;
    }
}

public sealed record ExecuteAutomationRequest
{
    public Guid? TenantId { get; init; }
    public Guid? ActorId { get; init; }
    public string? CorrelationId { get; init; }
    public string? Version { get; init; }
    public string? IdempotencyKey { get; init; }
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
