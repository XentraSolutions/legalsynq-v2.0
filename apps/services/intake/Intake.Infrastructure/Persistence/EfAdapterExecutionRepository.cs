using Intake.Application.Snapshot;
using Intake.Domain.Snapshot;
using Microsoft.EntityFrameworkCore;

namespace Intake.Infrastructure.Persistence;

public sealed class EfAdapterExecutionRepository(IntakeDbContext db)
    : IAdapterExecutionRepository
{
    public Task<IntakeAdapterExecution?> FindAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken cancellationToken) =>
        db.IntakeAdapterExecutions
            .Include(item => item.ExternalReferences)
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == executionId,
                cancellationToken);

    public Task<IntakeAdapterExecution?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken) =>
        db.IntakeAdapterExecutions
            .Include(item => item.ExternalReferences)
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.ExecutionKey == executionKey,
                cancellationToken);

    public async Task<IReadOnlyList<IntakeAdapterExecution>> ListBySnapshotAsync(
        Guid tenantId,
        Guid snapshotId,
        CancellationToken cancellationToken) =>
        await db.IntakeAdapterExecutions
            .Include(item => item.ExternalReferences)
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.SnapshotId == snapshotId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<AdapterExecutionClaim> TryClaimAsync(
        Guid tenantId,
        Guid snapshotId,
        string adapterCode,
        string adapterVersion,
        string executionKey,
        string idempotencyKey,
        Guid requestedByUserId,
        bool retry,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var execution = await db.IntakeAdapterExecutions
            .Include(item => item.ExternalReferences)
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.ExecutionKey == executionKey,
                cancellationToken);
        if (execution is null)
        {
            var now = DateTimeOffset.UtcNow;
            execution = new IntakeAdapterExecution
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                SnapshotId = snapshotId,
                AdapterCode = adapterCode,
                AdapterVersion = adapterVersion,
                ExecutionKey = executionKey,
                IdempotencyKey = idempotencyKey,
                ClaimToken = Guid.NewGuid().ToString("N"),
                RequestedByUserId = requestedByUserId,
                RequestedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.IntakeAdapterExecutions.Add(execution);
        }
        else
        {
            var recoverableProcessing =
                execution.Status == IntakeAdapterExecutionStatuses.Processing &&
                execution.StartedAt < DateTimeOffset.UtcNow.AddMinutes(-10);
            var canClaim = execution.Status == IntakeAdapterExecutionStatuses.Pending ||
                           execution.Status == IntakeAdapterExecutionStatuses.Retryable ||
                           (retry && execution.Status is
                               IntakeAdapterExecutionStatuses.Failed or
                               IntakeAdapterExecutionStatuses.Cancelled) ||
                           recoverableProcessing;
            if (!canClaim || execution.AttemptNumber >= maxAttempts)
            {
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                return new(execution, false);
            }
            execution.Status = IntakeAdapterExecutionStatuses.Processing;
            execution.AttemptNumber++;
            execution.ClaimToken = Guid.NewGuid().ToString("N");
            execution.StartedAt = DateTimeOffset.UtcNow;
            execution.CompletedAt = null;
            execution.FailureCode = null;
            execution.FailureMessage = null;
            execution.Version++;
            execution.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (execution.Status == IntakeAdapterExecutionStatuses.Pending)
        {
            execution.Status = IntakeAdapterExecutionStatuses.Processing;
            execution.AttemptNumber = 1;
            execution.StartedAt = DateTimeOffset.UtcNow;
            execution.ClaimToken = Guid.NewGuid().ToString("N");
        }
        execution.UpdatedAt = DateTimeOffset.UtcNow;
        execution.Attempts.Add(new IntakeAdapterExecutionAttempt
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            AdapterExecutionId = execution.Id,
            AttemptNumber = execution.AttemptNumber,
            Status = IntakeAdapterExecutionStatuses.Processing,
            StartedAt = execution.StartedAt ?? DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return new(execution, true);
        }
        catch (DbUpdateException exception)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            var concurrent = await FindByExecutionKeyAsync(
                tenantId,
                executionKey,
                cancellationToken);
            if (concurrent is not null)
                return new(concurrent, false);
            throw new InvalidOperationException("The adapter execution could not be claimed.", exception);
        }
    }

    public async Task FinalizeAsync(
        Guid tenantId,
        Guid executionId,
        string claimToken,
        int attemptNumber,
        string status,
        string? failureCode,
        string? failureMessage,
        string resultJson,
        IReadOnlyList<AdapterExternalReference> externalReferences,
        CancellationToken cancellationToken)
    {
        var execution = await db.IntakeAdapterExecutions
            .Include(item => item.Attempts)
            .Include(item => item.ExternalReferences)
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId &&
                        item.Id == executionId &&
                        item.Status == IntakeAdapterExecutionStatuses.Processing &&
                        item.ClaimToken == claimToken &&
                        item.AttemptNumber == attemptNumber,
                cancellationToken);
        if (execution is null)
            return;
        execution.Status = status;
        execution.FailureCode = failureCode;
        execution.FailureMessage = failureMessage;
        execution.ResultJson = resultJson;
        execution.CompletedAt = DateTimeOffset.UtcNow;
        execution.UpdatedAt = DateTimeOffset.UtcNow;
        execution.Version++;
        var attempt = execution.Attempts
            .OrderByDescending(item => item.AttemptNumber)
            .FirstOrDefault();
        if (attempt is not null)
        {
            attempt.Status = status;
            attempt.FailureCode = failureCode;
            attempt.FailureMessage = failureMessage;
            attempt.CompletedAt = DateTimeOffset.UtcNow;
        }
        foreach (var reference in execution.ExternalReferences.ToArray())
            db.IntakeAdapterExternalReferences.Remove(reference);
        foreach (var reference in externalReferences)
            execution.ExternalReferences.Add(new IntakeAdapterExternalReference
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                AdapterExecutionId = execution.Id,
                ReferenceType = reference.ReferenceType,
                ReferenceId = reference.ReferenceId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        await db.SaveChangesAsync(cancellationToken);
    }
}