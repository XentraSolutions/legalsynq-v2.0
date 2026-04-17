using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-MERGE-P5 — shared minimal-API helper that maps the three
/// execution passthrough endpoints onto a product's existing
/// <c>/api/.../{id:guid}/workflows</c> route group:
/// <list type="bullet">
///   <item><c>GET  ./{workflowInstanceId:guid}</c></item>
///   <item><c>POST ./{workflowInstanceId:guid}/advance</c></item>
///   <item><c>POST ./{workflowInstanceId:guid}/complete</c></item>
/// </list>
///
/// <para>
/// Caller passes <paramref name="productSlug"/> and
/// <paramref name="sourceEntityType"/> (matching the product's
/// <c>StartWorkflow</c> registration) so this helper can enforce
/// **parent-ownership**: each request first asks Flow for the workflows
/// of <c>{id}</c> and 404s if the supplied <c>{workflowInstanceId}</c>
/// is not in that list. This closes the IDOR gap where a
/// known-but-unrelated workflow id could otherwise be advanced via
/// any parent route in the same tenant.
/// </para>
///
/// <para>
/// Errors funnel through <see cref="FlowEndpointResults.MapFailure"/>
/// (503 on Flow downtime; 4xx propagated from the upstream).
/// </para>
/// </summary>
public static class FlowExecutionEndpoints
{
    public sealed class AdvanceWorkflowBody
    {
        public string ExpectedCurrentStepKey { get; set; } = string.Empty;
        public string? ToStepKey { get; set; }
        public Dictionary<string, string>? Payload { get; set; }
    }

    public static RouteGroupBuilder MapFlowExecutionPassthrough(
        this RouteGroupBuilder group,
        string productSlug,
        string sourceEntityType)
    {
        // ---- ownership check shared by all three handlers ------------------
        async Task<IResult?> EnsureOwnedAsync(
            Guid parentId, Guid workflowInstanceId, IFlowClient flow, CancellationToken ct)
        {
            try
            {
                var rows = await flow.ListBySourceEntityAsync(
                    productSlug, sourceEntityType, parentId.ToString(), ct);
                var owned = rows.Any(r => r.WorkflowInstanceId == workflowInstanceId);
                if (!owned)
                {
                    // 404 (not 403) so we don't disclose that the workflow
                    // exists under a different parent within the tenant.
                    return Results.NotFound(new
                    {
                        error = "Workflow instance is not associated with this resource.",
                        code  = "workflow_instance_not_owned"
                    });
                }
                return null;
            }
            catch (Exception ex) when (ex is FlowClientUnavailableException or HttpRequestException)
            {
                return FlowEndpointResults.MapFailure(ex);
            }
        }

        group.MapGet("/{workflowInstanceId:guid}", async (
            Guid id,
            Guid workflowInstanceId,
            IFlowClient flow,
            CancellationToken ct) =>
        {
            var ownership = await EnsureOwnedAsync(id, workflowInstanceId, flow, ct);
            if (ownership is not null) return ownership;

            try
            {
                var dto = await flow.GetWorkflowInstanceAsync(workflowInstanceId, ct);
                return Results.Ok(dto);
            }
            catch (Exception ex) when (ex is FlowClientUnavailableException or HttpRequestException)
            {
                return FlowEndpointResults.MapFailure(ex);
            }
        });

        group.MapPost("/{workflowInstanceId:guid}/advance", async (
            Guid id,
            Guid workflowInstanceId,
            [FromBody] AdvanceWorkflowBody body,
            IFlowClient flow,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.ExpectedCurrentStepKey))
            {
                return Results.BadRequest(new { error = "ExpectedCurrentStepKey is required." });
            }

            var ownership = await EnsureOwnedAsync(id, workflowInstanceId, flow, ct);
            if (ownership is not null) return ownership;

            try
            {
                var dto = await flow.AdvanceWorkflowAsync(workflowInstanceId, new FlowAdvanceWorkflowRequest
                {
                    ExpectedCurrentStepKey = body.ExpectedCurrentStepKey,
                    ToStepKey = body.ToStepKey,
                    Payload = body.Payload
                }, ct);
                return Results.Ok(dto);
            }
            catch (Exception ex) when (ex is FlowClientUnavailableException or HttpRequestException)
            {
                return FlowEndpointResults.MapFailure(ex);
            }
        });

        group.MapPost("/{workflowInstanceId:guid}/complete", async (
            Guid id,
            Guid workflowInstanceId,
            IFlowClient flow,
            CancellationToken ct) =>
        {
            var ownership = await EnsureOwnedAsync(id, workflowInstanceId, flow, ct);
            if (ownership is not null) return ownership;

            try
            {
                var dto = await flow.CompleteWorkflowAsync(workflowInstanceId, ct);
                return Results.Ok(dto);
            }
            catch (Exception ex) when (ex is FlowClientUnavailableException or HttpRequestException)
            {
                return FlowEndpointResults.MapFailure(ex);
            }
        });

        return group;
    }
}
