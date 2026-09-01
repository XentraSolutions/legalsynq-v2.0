using System.Collections.Concurrent;
using CareConnect.Application.Interfaces;

namespace CareConnect.Infrastructure.Services;

/// <summary>
/// Process-lifetime in-process cache for tenant platform host details.
/// Registered as Singleton — survives across DI scopes and request lifetimes.
/// Backed by ConcurrentDictionary for safe concurrent access across multiple request threads.
/// </summary>
public sealed class TenantSubdomainCache : ITenantSubdomainCache
{
    private readonly ConcurrentDictionary<Guid, TenantHostResult> _cache = new();

    public bool TryGetValue(Guid tenantId, out TenantHostResult? host)
    {
        var found = _cache.TryGetValue(tenantId, out var value);
        host = value;
        return found;
    }

    public void TryAdd(Guid tenantId, TenantHostResult host) =>
        _cache.TryAdd(tenantId, host);
}
