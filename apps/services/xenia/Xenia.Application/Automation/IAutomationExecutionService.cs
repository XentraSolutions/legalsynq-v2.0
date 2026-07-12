using Xenia.Application.Automation.Models;

namespace Xenia.Application.Automation;

/// <summary>
/// Routes execution requests to the correct provider and tracks execution lifecycle.
/// </summary>
public interface IAutomationExecutionService
{
    Task<AutomationExecutionResult> ExecuteAsync(
        AutomationExecutionRequest request,
        CancellationToken ct = default);

    Task<bool> CancelAsync(
        string automationKey,
        Guid executionId,
        Guid? tenantId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AutomationExecutionMetadata>> GetExecutionHistoryAsync(
        string? automationKey,
        Guid? tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<AutomationExecutionMetadata?> GetExecutionAsync(
        Guid executionId,
        Guid? tenantId,
        CancellationToken ct = default);
}
