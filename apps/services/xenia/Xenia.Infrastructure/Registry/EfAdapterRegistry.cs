using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Adapters;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Domain.Adapters;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Registry;

/// <summary>
/// EF Core-backed adapter registry.
/// Reflects the DI-registered adapter implementations into the database for observability.
/// Health check records are upserted on demand.
/// </summary>
internal sealed class EfAdapterRegistry : IAdapterRegistry
{
    private readonly XeniaDbContext _db;
    private readonly ILogger<EfAdapterRegistry> _logger;

    private static readonly (string Key, AdapterType Type, string Name) [] AdapterMeta =
    [
        ("tenant",       AdapterType.Tenant,       "Tenant Adapter"),
        ("identity",     AdapterType.Identity,     "Identity Adapter"),
        ("document",     AdapterType.Document,     "Document Adapter"),
        ("audit",        AdapterType.Audit,        "Audit Adapter"),
        ("notification", AdapterType.Notification, "Notification Adapter"),
        ("storage",      AdapterType.Storage,      "Storage Adapter"),
        ("workflow",     AdapterType.Workflow,      "Workflow Adapter"),
        ("ai",           AdapterType.Ai,           "AI Adapter"),
    ];

    public EfAdapterRegistry(XeniaDbContext db, ILogger<EfAdapterRegistry> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AdapterDto>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureAdaptersSeededAsync(ct);

        var adapters = await _db.PlatformAdapters
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return adapters.Select(AdapterDto.FromEntity).ToList();
    }

    public async Task<AdapterDto?> GetAsync(string adapterKey, CancellationToken ct = default)
    {
        var adapter = await _db.PlatformAdapters
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AdapterKey == adapterKey, ct);

        return adapter is null ? null : AdapterDto.FromEntity(adapter);
    }

    public async Task RecordHealthCheckAsync(
        string adapterKey,
        bool isAvailable,
        bool isHealthy,
        string? diagnosticMessage = null,
        CancellationToken ct = default)
    {
        var adapter = await _db.PlatformAdapters
            .FirstOrDefaultAsync(a => a.AdapterKey == adapterKey, ct);

        if (adapter is null)
        {
            _logger.LogWarning(
                "Xenia: health check recorded for unknown adapter '{AdapterKey}'. Ignored.",
                adapterKey);
            return;
        }

        var health = isHealthy ? AdapterStatus.Healthy : AdapterStatus.Unavailable;
        var availability = isAvailable ? AdapterStatus.Healthy : AdapterStatus.Unavailable;
        adapter.RecordHealthCheck(health, availability, diagnosticMessage);

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureAdaptersSeededAsync(CancellationToken ct)
    {
        foreach (var (key, type, name) in AdapterMeta)
        {
            var exists = await _db.PlatformAdapters
                .AnyAsync(a => a.AdapterKey == key, ct);

            if (!exists)
            {
                var adapter = new PlatformAdapter(Guid.CreateVersion7(), key, type, name, "1.0.0");
                _db.PlatformAdapters.Add(adapter);

                try { await _db.SaveChangesAsync(ct); }
                catch (Exception ex) when (ex.Message.Contains("Duplicate"))
                {
                    _db.Entry(adapter).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                }
            }
        }
    }
}
