using Xenia.Domain.Automation;

namespace Xenia.Application.Automation;

/// <summary>
/// Persistence for automation dead-letter entries.
///
/// Safety: no raw payloads, message bodies, credentials, or binaries stored.
/// </summary>
public interface IAutomationDeadLetterStore
{
    Task<AutomationDeadLetterEntry> CreateAsync(AutomationDeadLetterEntry entry, CancellationToken ct = default);
    Task<AutomationDeadLetterEntry?> GetAsync(Guid id, Guid? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationDeadLetterEntry>> ListAsync(string? automationKey, Guid? tenantId, AutomationDeadLetterStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<bool> RetryAsync(Guid id, Guid? tenantId, DateTime nextEligibleAt, CancellationToken ct = default);
    Task<bool> AbandonAsync(Guid id, Guid? tenantId, CancellationToken ct = default);
    Task<bool> ResolveAsync(Guid id, Guid? tenantId, CancellationToken ct = default);
}
