using System.Collections.Concurrent;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// Phase 1 in-memory DLQ store (bounded to 500 entries).
/// EF persistence is planned for Phase H.
/// </summary>
internal sealed class InMemoryAutomationDeadLetterStore : IAutomationDeadLetterStore
{
    private const int Cap = 500;
    private readonly ConcurrentDictionary<Guid, AutomationDeadLetterEntry> _store = new();

    public Task<AutomationDeadLetterEntry> CreateAsync(AutomationDeadLetterEntry entry, CancellationToken ct = default)
    {
        if (_store.Count >= Cap)
        {
            var oldest = _store.Values.OrderBy(e => e.FirstFailedAt).First();
            _store.TryRemove(oldest.Id, out _);
        }
        _store[entry.Id] = entry;
        return Task.FromResult(entry);
    }

    public Task<AutomationDeadLetterEntry?> GetAsync(Guid id, Guid? tenantId, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var e);
        if (e is null) return Task.FromResult<AutomationDeadLetterEntry?>(null);
        if (tenantId.HasValue && e.TenantId != tenantId) return Task.FromResult<AutomationDeadLetterEntry?>(null);
        return Task.FromResult<AutomationDeadLetterEntry?>(e);
    }

    public Task<IReadOnlyList<AutomationDeadLetterEntry>> ListAsync(
        string? automationKey, Guid? tenantId, AutomationDeadLetterStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = _store.Values.AsEnumerable();
        if (automationKey is not null)
            q = q.Where(e => e.AutomationKey.Equals(automationKey, StringComparison.OrdinalIgnoreCase));
        if (tenantId.HasValue)
            q = q.Where(e => e.TenantId == tenantId);
        if (status.HasValue)
            q = q.Where(e => e.Status == status.Value);
        IReadOnlyList<AutomationDeadLetterEntry> result = [.. q
            .OrderByDescending(e => e.FirstFailedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)];
        return Task.FromResult(result);
    }

    public Task<bool> RetryAsync(Guid id, Guid? tenantId, DateTime nextEligibleAt, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(id, out var e)) return Task.FromResult(false);
        if (tenantId.HasValue && e.TenantId != tenantId) return Task.FromResult(false);
        e.RecordRetryAttempt(nextEligibleAt);
        return Task.FromResult(true);
    }

    public Task<bool> AbandonAsync(Guid id, Guid? tenantId, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(id, out var e)) return Task.FromResult(false);
        if (tenantId.HasValue && e.TenantId != tenantId) return Task.FromResult(false);
        e.Abandon();
        return Task.FromResult(true);
    }

    public Task<bool> ResolveAsync(Guid id, Guid? tenantId, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(id, out var e)) return Task.FromResult(false);
        if (tenantId.HasValue && e.TenantId != tenantId) return Task.FromResult(false);
        e.MarkResolved();
        return Task.FromResult(true);
    }
}
