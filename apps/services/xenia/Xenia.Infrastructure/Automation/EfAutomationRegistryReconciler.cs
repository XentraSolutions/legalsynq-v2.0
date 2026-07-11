using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// EF Core–backed implementation of <see cref="IAutomationRegistryReconciler"/>.
///
/// Runs as a hosted service at startup, AFTER <see cref="AutomationRegistrationWorker"/>
/// has registered all discovered DI providers.
///
/// Reconciliation algorithm:
///   1. Load all discovered providers from <see cref="IAutomationRegistry.GetAllProviders()"/>.
///   2. Load all persisted rows from xn_automation_registry.
///   3. For each persisted row whose key is NOT in discovered providers:
///      - If not already Unavailable → MarkUnavailable (provider removed or disabled).
///   4. For each persisted row whose key IS in discovered providers and was Unavailable:
///      - Restore lifecycle to Registered (provider re-appeared after absence).
///      - The normal UpsertRegistrationAsync (in AutomationRegistrationWorker) handles
///        hash/version reconciliation — this reconciler only handles the Unavailable flag.
///   5. Each mutation is wrapped in an explicit per-row transaction for concurrency safety.
///
/// Idempotent: running twice produces the same final state.
/// Concurrency-safe: uses optimistic RowVersion; conflicting updates are retried once.
/// </summary>
internal sealed class EfAutomationRegistryReconciler : IAutomationRegistryReconciler, IHostedService
{
    private readonly IAutomationRegistry _registry;
    private readonly IDbContextFactory<XeniaDbContext> _contextFactory;
    private readonly ILogger<EfAutomationRegistryReconciler> _logger;

    private static readonly string InstanceId =
        $"{Environment.MachineName}:{Guid.CreateVersion7():N}";

    public EfAutomationRegistryReconciler(
        IAutomationRegistry registry,
        IDbContextFactory<XeniaDbContext> contextFactory,
        ILogger<EfAutomationRegistryReconciler> logger)
    {
        _registry       = registry;
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var summary = await ReconcileAsync(cancellationToken);
            _logger.LogInformation(
                "Registry reconciliation complete: " +
                "inserted={Ins} updated={Upd} unavailable={Unavail} restored={Rest} unchanged={Unch} " +
                "instance={InstanceId}",
                summary.Inserted, summary.Updated, summary.MarkedUnavailable,
                summary.Restored, summary.Unchanged, summary.InstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registry reconciliation failed — service continues");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task<ReconciliationSummary> ReconcileAsync(CancellationToken ct = default)
    {
        var reconciledAt  = DateTime.UtcNow;
        var discoveredKeys = new HashSet<string>(
            _registry.GetAllProviders().Select(p => p.AutomationKey),
            StringComparer.OrdinalIgnoreCase);

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var persistedRows = await ctx.AutomationRegistry.ToListAsync(ct);

        int inserted = 0, updated = 0, markedUnavailable = 0, restored = 0, unchanged = 0;

        foreach (var row in persistedRows)
        {
            var isDiscovered = discoveredKeys.Contains(row.AutomationKey);

            if (!isDiscovered && row.LifecycleStatus != AutomationLifecycleState.Unavailable)
            {
                // Provider is in DB but no longer in DI → mark unavailable
                var didChange = await MarkUnavailableWithRetryAsync(row.Id, reconciledAt, ct);
                if (didChange)
                    markedUnavailable++;
                else
                    unchanged++;
            }
            else if (isDiscovered && row.LifecycleStatus == AutomationLifecycleState.Unavailable)
            {
                // Provider was Unavailable but has returned → restore
                var didChange = await RestoreFromUnavailableAsync(row.Id, reconciledAt, ct);
                if (didChange)
                    restored++;
                else
                    unchanged++;
            }
            else
            {
                unchanged++;
            }
        }

        _logger.LogDebug(
            "Reconciliation details: discoveredKeys={Cnt} persistedRows={PersistCnt}",
            discoveredKeys.Count, persistedRows.Count);

        return new ReconciliationSummary
        {
            Inserted          = inserted,
            Updated           = updated,
            MarkedUnavailable = markedUnavailable,
            Restored          = restored,
            Unchanged         = unchanged,
            ReconciledAt      = reconciledAt,
            InstanceId        = InstanceId,
        };
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Marks a registry row as Unavailable. Uses explicit transaction + optimistic concurrency.
    /// Returns true if the row was changed, false if unchanged or conflict.
    /// </summary>
    private async Task<bool> MarkUnavailableWithRetryAsync(
        Guid registrationId, DateTime reconciledAt, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            await using var tx  = await ctx.Database.BeginTransactionAsync(ct);

            try
            {
                var row = await ctx.AutomationRegistry
                    .FirstOrDefaultAsync(r => r.Id == registrationId, ct);

                if (row is null || row.LifecycleStatus == AutomationLifecycleState.Unavailable)
                {
                    await tx.RollbackAsync(ct);
                    return false;
                }

                row.MarkUnavailable();
                ctx.AutomationRegistry.Update(row);
                await ctx.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _logger.LogWarning(
                    "Automation marked Unavailable — provider not found in DI: key={Key}",
                    row.AutomationKey);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await tx.RollbackAsync(CancellationToken.None);
                if (attempt == 0)
                {
                    _logger.LogDebug(ex,
                        "Concurrency conflict marking Unavailable for id={Id}, retrying",
                        registrationId);
                    continue;
                }
                _logger.LogWarning(ex,
                    "Concurrency conflict marking Unavailable for id={Id} — skipping", registrationId);
                return false;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(CancellationToken.None);
                _logger.LogWarning(ex,
                    "Failed to mark Unavailable for id={Id}", registrationId);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Restores an Unavailable registry row to Registered state.
    /// Uses explicit transaction + optimistic concurrency.
    /// Returns true if the row was restored, false if unchanged or conflict.
    /// </summary>
    private async Task<bool> RestoreFromUnavailableAsync(
        Guid registrationId, DateTime reconciledAt, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            await using var tx  = await ctx.Database.BeginTransactionAsync(ct);

            try
            {
                var row = await ctx.AutomationRegistry
                    .FirstOrDefaultAsync(r => r.Id == registrationId, ct);

                if (row is null || row.LifecycleStatus != AutomationLifecycleState.Unavailable)
                {
                    await tx.RollbackAsync(ct);
                    return false;
                }

                row.SetLifecycle(AutomationLifecycleState.Registered);
                row.Reconcile(row.CurrentVersion, row.ManifestHash, reconciledAt);
                ctx.AutomationRegistry.Update(row);
                await ctx.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _logger.LogInformation(
                    "Automation restored from Unavailable — provider rediscovered: key={Key}",
                    row.AutomationKey);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await tx.RollbackAsync(CancellationToken.None);
                if (attempt == 0)
                {
                    _logger.LogDebug(ex,
                        "Concurrency conflict restoring id={Id}, retrying", registrationId);
                    continue;
                }
                _logger.LogWarning(ex,
                    "Concurrency conflict restoring id={Id} — skipping", registrationId);
                return false;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(CancellationToken.None);
                _logger.LogWarning(ex, "Failed to restore id={Id}", registrationId);
                return false;
            }
        }

        return false;
    }
}
