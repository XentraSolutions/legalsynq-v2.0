using Intake.Application.Snapshot;
using Intake.Domain.Snapshot;
using Microsoft.EntityFrameworkCore;

namespace Intake.Infrastructure.Persistence;

public sealed class EfDocumentAssociationExecutionRepository(IDbContextFactory<IntakeDbContext> factory)
    : IDocumentAssociationExecutionRepository
{
    public async Task<DocumentAssociationExecution?> FindAsync(
        Guid tenantId, Guid executionId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.DocumentAssociationExecutions
            .AsNoTracking()
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == executionId, ct);
    }

    public async Task<IReadOnlyList<DocumentAssociationExecution>> ListAsync(
        Guid tenantId, Guid snapshotId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.DocumentAssociationExecutions
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.TenantId == tenantId && x.SnapshotId == snapshotId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<DocumentAssociationExecution?> FindByExecutionKeyAsync(
        Guid tenantId, string executionKey, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.DocumentAssociationExecutions
            .AsNoTracking()
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ExecutionKey == executionKey, ct);
    }

    public async Task SaveAsync(DocumentAssociationExecution execution, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.DocumentAssociationExecutions
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.TenantId == execution.TenantId && x.Id == execution.Id, ct);
        if (existing is null)
            db.DocumentAssociationExecutions.Add(execution);
        else
        {
            db.Entry(existing).CurrentValues.SetValues(execution);
            foreach (var item in execution.Items)
            {
                var persisted = existing.Items.SingleOrDefault(x => x.Id == item.Id);
                if (persisted is null) existing.Items.Add(item);
                else db.Entry(persisted).CurrentValues.SetValues(item);
            }
        }
        await db.SaveChangesAsync(ct);
    }

}