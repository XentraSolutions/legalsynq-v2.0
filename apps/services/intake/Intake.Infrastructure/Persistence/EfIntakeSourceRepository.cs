using Intake.Application.Configuration;
using Intake.Application.Sources;
using Intake.Domain.Sources;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Intake.Infrastructure.Persistence;

public sealed class EfIntakeSourceRepository(IntakeDbContext db)
    : IIntakeSourceRepository
{
    public async Task<IReadOnlyList<TenantIntakeSource>> ListTenantSourcesAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await db.TenantIntakeSources
            .AsNoTracking()
            .Where(source => source.TenantId == tenantId)
            .ToListAsync(cancellationToken);

    public Task<TenantIntakeSource?> FindTenantSourceAsync(
        Guid tenantId,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        db.TenantIntakeSources.SingleOrDefaultAsync(
            source => source.TenantId == tenantId && source.Id == sourceId,
            cancellationToken);

    public Task<TenantIntakeSource?> FindByNormalizedEmailAddressAsync(
        string normalizedEmailAddress,
        CancellationToken cancellationToken) =>
        db.TenantIntakeSources.SingleOrDefaultAsync(
            source => source.NormalizedEmailAddress == normalizedEmailAddress,
            cancellationToken);

    public async Task<IReadOnlyList<TenantIntakeSource>> ListTenantPurposeSourcesAsync(
        Guid tenantId,
        string purpose,
        CancellationToken cancellationToken) =>
        await db.TenantIntakeSources
            .Where(source => source.TenantId == tenantId && source.Purpose == purpose)
            .ToListAsync(cancellationToken);

    public void Add(TenantIntakeSource source) =>
        db.TenantIntakeSources.Add(source);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw IntakeConfigurationException.Conflict(
                "STALE_SOURCE_CONFIGURATION_VERSION",
                "The source was changed by another request. Reload it and retry with the current configurationVersion.");
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            throw IntakeConfigurationException.Conflict(
                "SOURCE_CONSTRAINT_CONFLICT",
                "The requested source conflicts with an existing email owner or default source.");
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 } ||
        exception.InnerException?.InnerException is MySqlException { Number: 1062 };
}