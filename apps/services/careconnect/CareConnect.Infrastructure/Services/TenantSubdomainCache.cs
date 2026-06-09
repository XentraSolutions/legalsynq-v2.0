using System.Collections.Concurrent;
using CareConnect.Application.Interfaces;

namespace CareConnect.Infrastructure.Services;

/// <summary>
/// Process-lifetime in-process cache for tenant subdomain slugs.
/// Registered as Singleton — survives across DI scopes and request lifetimes.
/// Backed by ConcurrentDictionary for safe concurrent access across multiple request threads.
/// </summary>
public sealed class TenantSubdomainCache : ITenantSubdomainCache
{
    private readonly ConcurrentDictionary<Guid, string> _cache = new();

    public bool TryGetValue(Guid tenantId, out string? subdomain)
    {
        var found = _cache.TryGetValue(tenantId, out var value);
        subdomain = value;
        return found;
    }

    public void TryAdd(Guid tenantId, string subdomain) =>
        _cache.TryAdd(tenantId, subdomain);
}
