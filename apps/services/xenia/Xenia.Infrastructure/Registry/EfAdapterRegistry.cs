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
///
/// Adapter criticality classification:
///   Mandatory — Xenia cannot serve requests without this adapter. DB, Tenant, and Identity
///               adapters are mandatory; their unavailability causes /ready → 503.
///   Optional  — Xenia degrades gracefully; Documents, Storage, Notification, Workflow, AI.
///   Disabled  — Intentionally not wired; status ignored by readiness computation.
/// </summary>
internal sealed class EfAdapterRegistry : IAdapterRegistry
{
    private readonly XeniaDbContext _db;
    private readonly ILogger<EfAdapterRegistry> _logger;

    /// <summary>
    /// Canonical adapter metadata including criticality classification.
    ///
    /// Mandatory:
    ///   - tenant       — required to resolve and validate tenant context for every request.
    ///   - identity     — required for user identity validation and permission resolution.
    ///
    /// Optional (graceful degradation):
    ///   - document     — file operations; modules fall back to error response when absent.
    ///   - audit        — event recording; falls back to log-only when absent (see UnavailableAuditAdapter).
    ///   - notification — outbound messaging; optional by design.
    ///   - storage      — binary object storage; optional by design.
    ///   - workflow     — workflow integration; optional by design.
    ///   - ai           — AI inference integration; optional by design.
    /// </summary>
    private static readonly (string Key, AdapterType Type, string Name, AdapterCriticality Criticality)[] AdapterMeta =
    [
        ("tenant",       AdapterType.Tenant,       "Tenant Adapter",       AdapterCriticality.Mandatory),
        ("identity",     AdapterType.Identity,     "Identity Adapter",     AdapterCriticality.Mandatory),
        ("document",     AdapterType.Document,     "Document Adapter",     AdapterCriticality.Optional),
        ("audit",        AdapterType.Audit,        "Audit Adapter",        AdapterCriticality.Optional),
        ("notification", AdapterType.Notification, "Notification Adapter", AdapterCriticality.Optional),
        ("storage",      AdapterType.Storage,      "Storage Adapter",      AdapterCriticality.Optional),
        ("workflow",     AdapterType.Workflow,      "Workflow Adapter",    AdapterCriticality.Optional),
        ("ai",           AdapterType.Ai,           "AI Adapter",           AdapterCriticality.Optional),
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
        foreach (var (key, type, name, criticality) in AdapterMeta)
        {
            var existing = await _db.PlatformAdapters
                .FirstOrDefaultAsync(a => a.AdapterKey == key, ct);

            if (existing is null)
            {
                var adapter = new PlatformAdapter(Guid.CreateVersion7(), key, type, name, "1.0.0", criticality);
                _db.PlatformAdapters.Add(adapter);

                try { await _db.SaveChangesAsync(ct); }
                catch (Exception ex) when (ex.Message.Contains("Duplicate"))
                {
                    _db.Entry(adapter).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                }
            }
            else if (existing.Criticality != criticality)
            {
                // Correct criticality if it drifted (e.g. pre-criticality seed)
                existing.SetCriticality(criticality);
                await _db.SaveChangesAsync(ct);
            }
        }
    }
}
