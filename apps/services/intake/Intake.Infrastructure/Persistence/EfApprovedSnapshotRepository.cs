using Intake.Application.Snapshot;
using Intake.Domain.Snapshot;
using Microsoft.EntityFrameworkCore;

namespace Intake.Infrastructure.Persistence;

public sealed class EfApprovedSnapshotRepository(IntakeDbContext db)
    : IApprovedSnapshotRepository
{
    public Task<ApprovedSnapshotSchemaDefinition?> FindSchemaAsync(
        string code,
        int version,
        CancellationToken cancellationToken) =>
        db.ApprovedSnapshotSchemaDefinitions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Code == code && item.Version == version,
            cancellationToken);

    public Task<ApprovedIntakeSnapshot?> FindAsync(
        Guid tenantId,
        Guid snapshotId,
        CancellationToken cancellationToken) =>
        db.ApprovedIntakeSnapshots.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == snapshotId,
            cancellationToken);

    public Task<ApprovedIntakeSnapshot?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken) =>
        db.ApprovedIntakeSnapshots.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ExecutionKey == executionKey,
            cancellationToken);

    public Task<ApprovedIntakeSnapshot?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        db.ApprovedIntakeSnapshots.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == tenantId &&
                    item.ArtifactId == artifactId &&
                    item.IsCurrent &&
                    item.Status == ApprovedSnapshotStatuses.Ready,
            cancellationToken);

    public async Task<(IReadOnlyList<ApprovedIntakeSnapshot> Items, long TotalCount)> ListByArtifactAsync(
        Guid tenantId,
        Guid artifactId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.ApprovedIntakeSnapshots.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ArtifactId == artifactId);
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.SnapshotVersion)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<ApprovedIntakeSnapshot> PersistReadyAsync(
        ApprovedIntakeSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var existing = await db.ApprovedIntakeSnapshots
            .SingleOrDefaultAsync(
                item => item.TenantId == snapshot.TenantId &&
                        item.ExecutionKey == snapshot.ExecutionKey,
                cancellationToken);
        if (existing is not null)
        {
            await transaction?.CommitAsync(cancellationToken)!;
            return existing;
        }

        var current = await db.ApprovedIntakeSnapshots
            .Where(item => item.TenantId == snapshot.TenantId &&
                          item.ArtifactId == snapshot.ArtifactId &&
                          item.IsCurrent)
            .OrderByDescending(item => item.SnapshotVersion)
            .FirstOrDefaultAsync(cancellationToken);
        if (current is not null)
        {
            if (current.SnapshotVersion >= snapshot.SnapshotVersion)
                throw new SnapshotVersionConflictException();
            current.IsCurrent = false;
            current.ActiveCurrentKey = null;
            current.Status = ApprovedSnapshotStatuses.Superseded;
            current.UpdatedAt = DateTimeOffset.UtcNow;
            snapshot.SupersedesSnapshotId = current.Id;
            await db.SaveChangesAsync(cancellationToken);
        }

        snapshot.IsCurrent = true;
        snapshot.Status = ApprovedSnapshotStatuses.Ready;
        db.ApprovedIntakeSnapshots.Add(snapshot);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return snapshot;
        }
        catch (DbUpdateException exception)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            throw new InvalidOperationException("The approved snapshot key already exists.", exception);
        }
    }
}