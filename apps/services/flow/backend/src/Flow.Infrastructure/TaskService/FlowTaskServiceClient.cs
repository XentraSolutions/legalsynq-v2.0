using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flow.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.TaskService;

/// <summary>
/// TASK-FLOW-01 — HTTP implementation of <see cref="IFlowTaskServiceClient"/>.
///
/// <para>
/// Calls the canonical Task service on behalf of Flow. All mutations here
/// make Task service the write authority; the caller is responsible for
/// mirroring the change to <c>flow_workflow_tasks</c> (the shadow) after
/// this client returns.
/// </para>
///
/// <para>
/// Status mapping (Flow → Task service):
///   Open        → OPEN
///   InProgress  → IN_PROGRESS
///   Completed   → COMPLETED
///   Cancelled   → CANCELLED
/// </para>
///
/// <para>
/// Assignment model (Phase 1):
///   DirectUser  → <c>assignedUserId</c> set on the Task service record.
///   RoleQueue / OrgQueue / Unassigned → <c>assignedUserId = null</c> on
///     Task service (no native role/org-queue concept); metadata preserved
///     in the shadow table only. Logged as a warning.
/// </para>
/// </summary>
public sealed class FlowTaskServiceClient : IFlowTaskServiceClient
{
    private const string SourceProductCode = "SYNQ_FLOW";
    private const string TaskScope         = "WORKFLOW";

    private static readonly JsonSerializerOptions _json =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient                    _http;
    private readonly ILogger<FlowTaskServiceClient> _log;

    public FlowTaskServiceClient(
        HttpClient                    http,
        ILogger<FlowTaskServiceClient> log)
    {
        _http = http;
        _log  = log;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<Guid> CreateWorkflowTaskAsync(
        Guid      workflowInstanceId,
        string    stepKey,
        string    title,
        string    priority,
        DateTime? dueAt,
        string?   assignedUserId,
        CancellationToken ct = default)
    {
        Guid? assignedUserGuid = null;
        if (!string.IsNullOrWhiteSpace(assignedUserId))
        {
            if (Guid.TryParse(assignedUserId, out var parsed))
                assignedUserGuid = parsed;
            else
                _log.LogWarning(
                    "FlowTaskServiceClient.Create: AssignedUserId '{UserId}' is not a valid Guid — will create task unassigned in Task service.",
                    assignedUserId);
        }

        var body = new
        {
            title              = title,
            priority           = MapPriority(priority),
            scope              = TaskScope,
            assignedUserId     = assignedUserGuid,
            sourceProductCode  = SourceProductCode,
            dueAt              = dueAt,
            workflowInstanceId = workflowInstanceId,
            workflowStepKey    = stepKey,
        };

        var req = BuildRequest(HttpMethod.Post, "/api/tasks", body);
        var resp = await _http.SendAsync(req, ct);
        EnsureSuccess(resp, "POST /api/tasks");

        var dto = await resp.Content.ReadFromJsonAsync<TaskIdDto>(_json, ct)
            ?? throw new InvalidOperationException(
                "Task service returned null body on task creation.");

        _log.LogInformation(
            "FlowTaskServiceClient.Create: created task {TaskId} for workflow={WorkflowInstanceId} step={StepKey}",
            dto.Id, workflowInstanceId, stepKey);

        return dto.Id;
    }

    // ── Status transitions ────────────────────────────────────────────────────

    public Task StartTaskAsync(Guid taskId, CancellationToken ct = default) =>
        TransitionAsync(taskId, "IN_PROGRESS", ct);

    public Task CompleteTaskAsync(Guid taskId, CancellationToken ct = default) =>
        TransitionAsync(taskId, "COMPLETED", ct);

    public Task CancelTaskAsync(Guid taskId, CancellationToken ct = default) =>
        TransitionAsync(taskId, "CANCELLED", ct);

    private async Task TransitionAsync(
        Guid   taskId,
        string newStatus,
        CancellationToken ct)
    {
        var body = new { status = newStatus };
        var req  = BuildRequest(HttpMethod.Post, $"/api/tasks/{taskId}/status", body);
        var resp = await _http.SendAsync(req, ct);
        EnsureSuccess(resp, $"POST /api/tasks/{taskId}/status ({newStatus})");

        _log.LogInformation(
            "FlowTaskServiceClient.Transition: task {TaskId} → {NewStatus}",
            taskId, newStatus);
    }

    // ── Assignment ────────────────────────────────────────────────────────────

    public async Task AssignUserAsync(
        Guid  taskId,
        Guid? assignedUserId,
        CancellationToken ct = default)
    {
        var body = new { assignedUserId };
        var req  = BuildRequest(HttpMethod.Post, $"/api/tasks/{taskId}/assign", body);
        var resp = await _http.SendAsync(req, ct);
        EnsureSuccess(resp, $"POST /api/tasks/{taskId}/assign");

        _log.LogInformation(
            "FlowTaskServiceClient.Assign: task {TaskId} → user {UserId}",
            taskId, assignedUserId?.ToString() ?? "(unassigned)");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps Flow priority strings to Task service canonical values.
    /// Flow uses PascalCase (Normal, High, Urgent, Low);
    /// Task service expects UPPER_CASE constants.
    /// </summary>
    private static string MapPriority(string? priority) =>
        priority?.ToUpperInvariant() switch
        {
            "URGENT" => "URGENT",
            "HIGH"   => "HIGH",
            "LOW"    => "LOW",
            _        => "NORMAL",
        };

    private HttpRequestMessage BuildRequest<TBody>(
        HttpMethod method,
        string     path,
        TBody?     body = default)
    {
        var req = new HttpRequestMessage(method, path);

        if (body is not null)
            req.Content = JsonContent.Create(body, options: _json);

        return req;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Task service call '{operation}' failed: {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    // ── Private DTOs (minimal projection for what we need) ───────────────────

    private sealed record TaskIdDto(Guid Id);
}
