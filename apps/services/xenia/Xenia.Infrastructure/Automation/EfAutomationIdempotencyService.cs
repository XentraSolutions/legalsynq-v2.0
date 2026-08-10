using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// EF Core–backed implementation of <see cref="IAutomationIdempotencyService"/>.
///
/// Uses the DB-level unique constraint on (tenant_id, automation_key, idempotency_key)
/// as the atomic fence against concurrent duplicate requests. If two concurrent calls
/// arrive with the same key, one INSERT succeeds and the other receives a
/// <see cref="DbUpdateException"/> with a duplicate-key error, which we surface as
/// <see cref="IdempotencyReservationResult.AlreadyExists"/>.
///
/// Singleton — uses <see cref="IDbContextFactory{T}"/> per operation.
/// </summary>
internal sealed class EfAutomationIdempotencyService : IAutomationIdempotencyService
{
    private readonly IDbContextFactory<XeniaDbContext> _contextFactory;
    private readonly ILogger<EfAutomationIdempotencyService> _logger;

    public EfAutomationIdempotencyService(
        IDbContextFactory<XeniaDbContext> contextFactory,
        ILogger<EfAutomationIdempotencyService> logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task<IdempotencyReservation> TryReserveAsync(
        Guid tenantId,
        string automationKey,
        string idempotencyKey,
        string requestFingerprint,
        DateTime expiresAt,
        CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await ctx.AutomationIdempotency
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.AutomationKey == automationKey &&
                r.IdempotencyKey == idempotencyKey, ct);

        if (existing is not null)
        {
            if (!existing.FingerprintMatches(requestFingerprint))
            {
                _logger.LogWarning(
                    "Idempotency fingerprint conflict: tenant={TenantId} key={Key} ikey={IKey}",
                    tenantId, automationKey, idempotencyKey);
                return IdempotencyReservation.Conflict();
            }
            return IdempotencyReservation.AlreadyExists(existing.ExecutionId);
        }

        var record = AutomationIdempotencyRecord.Reserve(
            tenantId, automationKey, idempotencyKey, requestFingerprint, expiresAt);

        ctx.AutomationIdempotency.Add(record);

        try
        {
            await ctx.SaveChangesAsync(ct);
            return IdempotencyReservation.Reserved();
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            var race = await ctx.AutomationIdempotency
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.TenantId == tenantId &&
                    r.AutomationKey == automationKey &&
                    r.IdempotencyKey == idempotencyKey, ct);

            if (race is not null && !race.FingerprintMatches(requestFingerprint))
                return IdempotencyReservation.Conflict();

            return IdempotencyReservation.AlreadyExists(race?.ExecutionId);
        }
    }

    public async Task<bool> BindExecutionAsync(
        Guid tenantId,
        string automationKey,
        string idempotencyKey,
        Guid executionId,
        CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var record = await ctx.AutomationIdempotency
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.AutomationKey == automationKey &&
                r.IdempotencyKey == idempotencyKey, ct);

        if (record is null) return false;
        if (record.ExecutionId is not null) return true;

        record.BindExecution(executionId);

        try
        {
            await ctx.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict binding execution {ExecId} to idempotency key={IKey}",
                executionId, idempotencyKey);
            return false;
        }
    }

    public async Task<AutomationIdempotencyRecord?> GetAsync(
        Guid tenantId,
        string automationKey,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        return await ctx.AutomationIdempotency
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.AutomationKey == automationKey &&
                r.IdempotencyKey == idempotencyKey, ct);
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;
}
