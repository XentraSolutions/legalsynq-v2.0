using Intake.Application.Operations;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Matching;
using Intake.Domain.Normalization;
using Intake.Domain.Operations;
using Intake.Domain.Policy;
using Intake.Domain.Review;
using Intake.Domain.Snapshot;
using Microsoft.EntityFrameworkCore;

namespace Intake.Infrastructure.Persistence;

public sealed class EfIntakeRecoveryRepository(
    IDbContextFactory<IntakeDbContext> factory,
    IntakeRecoveryOptions options) : IIntakeRecoveryRepository
{
    private static readonly string[] StalledStatuses =
        ["PENDING", "PROCESSING", "CREATING", "RETRYABLE"];

    public async Task<IReadOnlyList<RecoveryCandidate>> DiscoverAsync(
        DateTimeOffset staleBefore,
        int maxItems,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var candidates = new List<RecoveryCandidate>();

        candidates.AddRange(await db.InboundEmails.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore &&
                (StalledStatuses.Contains(x.ProcessingStatus) ||
                 StalledStatuses.Contains(x.CaptureStatus)))
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.EmailCapture, x.Id,
                string.IsNullOrEmpty(x.ProcessingStatus) ? x.CaptureStatus : x.ProcessingStatus,
                false, null, "Email capture remains in a non-terminal state.",
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        candidates.AddRange(await db.ManualIntakeSubmissions.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore && StalledStatuses.Contains(x.Status))
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.ArtifactProcessing, x.Id, x.Status,
                false, null, "Manual intake remains in a non-terminal state.",
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        candidates.AddRange(await db.IntakeArtifacts.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore && StalledStatuses.Contains(x.ProcessingStatus))
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.ArtifactProcessing, x.Id, x.ProcessingStatus,
                x.IsRetryable, x.FailureCode, x.FailureMessage,
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        candidates.AddRange(await db.ArtifactClassifications.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore && StalledStatuses.Contains(x.Status))
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.Classification, x.Id, x.Status,
                x.IsRetryable, x.FailureCode, x.FailureMessage,
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        candidates.AddRange(await db.ArtifactExtractions.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore && StalledStatuses.Contains(x.Status))
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.Extraction, x.Id, x.Status,
                x.IsRetryable, x.FailureCode, x.FailureMessage,
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        candidates.AddRange(await db.ArtifactNormalizations.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore && StalledStatuses.Contains(x.Status))
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.Normalization, x.Id, x.Status,
                false, x.FailureCode, x.FailureMessage,
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        candidates.AddRange(await db.ArtifactMatchRuns.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore && StalledStatuses.Contains(x.Status))
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.Matching, x.Id, x.Status,
                false, x.FailureCode, x.FailureMessage,
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        candidates.AddRange(await db.ArtifactPolicyEvaluations.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore && StalledStatuses.Contains(x.Status))
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.Policy, x.Id, x.Status,
                false, x.FailureCode, x.FailureMessage,
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        candidates.AddRange(await db.IntakeReviews.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore &&
                (x.Status == IntakeReviewStatuses.Pending ||
                 x.Status == IntakeReviewStatuses.InReview))
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.Review, x.Id, x.Status,
                false, null, "Review remains open beyond the stale-work threshold.",
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        candidates.AddRange(await db.ApprovedIntakeSnapshots.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore && x.Status == ApprovedSnapshotStatuses.Creating)
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.Snapshot, x.Id, x.Status,
                false, null, "Snapshot creation did not reach a terminal state.",
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        candidates.AddRange(await db.IntakeAdapterExecutions.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore &&
                (x.Status == IntakeAdapterExecutionStatuses.Processing ||
                 x.Status == IntakeAdapterExecutionStatuses.Retryable ||
                 x.Status == IntakeAdapterExecutionStatuses.Pending))
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.AdapterExecution, x.Id, x.Status,
                true, x.FailureCode, x.FailureMessage,
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        candidates.AddRange(await db.DocumentAssociationExecutions.AsNoTracking()
            .Where(x => x.UpdatedAt < staleBefore &&
                (x.Status == DocumentAssociationExecutionStatuses.Processing ||
                 x.Status == DocumentAssociationExecutionStatuses.Retryable ||
                 x.Status == DocumentAssociationExecutionStatuses.PartiallySucceeded ||
                 x.Status == DocumentAssociationExecutionStatuses.Pending))
            .OrderBy(x => x.UpdatedAt).Take(maxItems)
            .Select(x => new RecoveryCandidate(
                x.TenantId, IntakeRecoveryStages.DocumentAssociation, x.Id, x.Status,
                true, x.FailureCode, x.FailureMessage,
                x.UpdatedAt, x.CreatedAt)).ToListAsync(cancellationToken));

        return candidates.OrderBy(x => x.UpdatedAt).Take(Math.Clamp(maxItems, 1, 500)).ToArray();
    }

    public async Task<IntakeRecoveryWorkItem> EnsureDiscoveredAsync(
        RecoveryCandidate candidate,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.IntakeRecoveryWorkItems
            .Include(x => x.Attempts)
            .SingleOrDefaultAsync(x =>
                x.TenantId == candidate.TenantId &&
                x.Stage == candidate.Stage &&
                x.ObjectId == candidate.ObjectId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (existing is not null)
        {
            existing.DomainStatus = candidate.DomainStatus;
            existing.Retryable = candidate.Retryable;
            existing.LastFailureCode = candidate.FailureCode;
            existing.LastSafeMessage = FailureSanitizer.Message(
                candidate.SafeMessage, "The operation remains in a non-terminal state.");
            existing.UpdatedAt = now;
            if (existing.StaleSince is null)
                existing.StaleSince = candidate.UpdatedAt;
            if (existing.RecoveryStatus == IntakeRecoveryStatuses.Pending)
                existing.RecoveryStatus = IntakeRecoveryStatuses.Stale;
            await db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var item = new IntakeRecoveryWorkItem
        {
            Id = Guid.CreateVersion7(),
            TenantId = candidate.TenantId,
            Stage = candidate.Stage,
            ObjectId = candidate.ObjectId,
            DomainStatus = candidate.DomainStatus,
            RecoveryStatus = IntakeRecoveryStatuses.Stale,
            Retryable = candidate.Retryable,
            LastFailureCode = candidate.FailureCode,
            LastSafeMessage = FailureSanitizer.Message(
                candidate.SafeMessage, "The operation remains in a non-terminal state."),
            FailureCategory = candidate.FailureCode is null
                ? IntakeFailureCategories.Unknown
                : IntakeFailureCategories.Dependency,
            StaleSince = candidate.UpdatedAt,
            CorrelationId = candidate.CorrelationId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.IntakeRecoveryWorkItems.Add(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return item;
        }
        catch (DbUpdateException)
        {
            return await db.IntakeRecoveryWorkItems
                .Include(x => x.Attempts)
                .SingleAsync(x =>
                    x.TenantId == candidate.TenantId &&
                    x.Stage == candidate.Stage &&
                    x.ObjectId == candidate.ObjectId, cancellationToken);
        }
    }

    public async Task<IntakeRecoveryWorkItem?> FindAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.IntakeRecoveryWorkItems
            .Include(x => x.Attempts.OrderByDescending(a => a.AttemptNumber))
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == workItemId, cancellationToken);
    }

    public async Task<IReadOnlyList<IntakeRecoveryWorkItem>> ListEligibleAsync(
        Guid? tenantId,
        DateTimeOffset now,
        int maxItems,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.IntakeRecoveryWorkItems.AsNoTracking()
            .Where(x => (tenantId == null || x.TenantId == tenantId) &&
                (x.RecoveryStatus == IntakeRecoveryStatuses.Stale ||
                 x.RecoveryStatus == IntakeRecoveryStatuses.Pending ||
                 x.RecoveryStatus == IntakeRecoveryStatuses.Retryable) &&
                x.Retryable &&
                (x.NextRetryAt == null || x.NextRetryAt <= now))
            .OrderBy(x => x.StaleSince ?? x.UpdatedAt)
            .Take(Math.Clamp(maxItems, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<IntakeRecoveryWorkItem?> TryClaimAsync(
        Guid tenantId,
        Guid workItemId,
        DateTimeOffset now,
        int maxAttempts,
        bool manual,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var claimToken = Guid.NewGuid().ToString("N");
        var query = db.IntakeRecoveryWorkItems.Where(x =>
            x.TenantId == tenantId &&
            x.Id == workItemId &&
            x.RecoveryStatus != IntakeRecoveryStatuses.Processing &&
            x.RecoveryStatus != IntakeRecoveryStatuses.Recovered &&
            x.RecoveryStatus != IntakeRecoveryStatuses.Cancelled &&
            x.RecoveryStatus != IntakeRecoveryStatuses.Exhausted &&
            x.AttemptCount < maxAttempts &&
            (manual || x.NextRetryAt == null || x.NextRetryAt <= now) &&
            (x.Retryable || x.RecoveryStatus == IntakeRecoveryStatuses.Stale ||
             x.RecoveryStatus == IntakeRecoveryStatuses.Pending));
        var changed = await query.ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.RecoveryStatus, IntakeRecoveryStatuses.Processing)
            .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
            .SetProperty(x => x.LastRecoveryAttemptAt, now)
            .SetProperty(x => x.ClaimedAt, now)
            .SetProperty(x => x.ClaimToken, claimToken)
            .SetProperty(x => x.RecoverySource, manual ? "MANUAL" : "AUTOMATIC")
            .SetProperty(x => x.Version, x => x.Version + 1)
            .SetProperty(x => x.UpdatedAt, now), cancellationToken);
        if (changed != 1)
            return null;

        var item = await db.IntakeRecoveryWorkItems
            .Include(x => x.Attempts)
            .SingleAsync(x => x.TenantId == tenantId && x.Id == workItemId, cancellationToken);
        item.Attempts.Add(new IntakeRecoveryAttempt
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            WorkItemId = item.Id,
            AttemptNumber = item.AttemptCount,
            Status = IntakeRecoveryStatuses.Processing,
            RecoverySource = manual ? "MANUAL" : "AUTOMATIC",
            StartedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task CompleteAsync(
        IntakeRecoveryWorkItem item,
        RecoveryHandlerResult result,
        DateTimeOffset? nextRetryAt,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var persisted = await db.IntakeRecoveryWorkItems
            .Include(x => x.Attempts)
            .SingleOrDefaultAsync(x =>
                x.TenantId == item.TenantId &&
                x.Id == item.Id &&
                x.RecoveryStatus == IntakeRecoveryStatuses.Processing &&
                x.ClaimToken == item.ClaimToken, cancellationToken);
        if (persisted is null)
            return;

        var exhausted = result.Retryable && persisted.AttemptCount >=
            Math.Clamp(options.MaxRecoveryAttempts, 1, 50);
        persisted.RecoveryStatus = exhausted
            ? IntakeRecoveryStatuses.Exhausted
            : result.Recovered
                ? IntakeRecoveryStatuses.Recovered
                : result.Retryable
                    ? IntakeRecoveryStatuses.Retryable
                    : IntakeRecoveryStatuses.Failed;
        persisted.LastFailureCode = result.FailureCode;
        persisted.LastSafeMessage = FailureSanitizer.Message(
            result.SafeMessage, "The recovery operation did not complete.");
        persisted.FailureCategory = result.FailureCategory;
        persisted.NextRetryAt = exhausted ? null : nextRetryAt;
        persisted.ExhaustedAt = exhausted ? DateTimeOffset.UtcNow : null;
        persisted.ClaimToken = null;
        persisted.ClaimedAt = null;
        persisted.UpdatedAt = DateTimeOffset.UtcNow;
        persisted.Version++;
        var attempt = persisted.Attempts.OrderByDescending(x => x.AttemptNumber).FirstOrDefault();
        if (attempt is not null)
        {
            attempt.Status = persisted.RecoveryStatus;
            attempt.FailureCode = result.FailureCode;
            attempt.SafeMessage = persisted.LastSafeMessage;
            attempt.FailureCategory = result.FailureCategory;
            attempt.CompletedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> CancelAsync(
        Guid tenantId,
        Guid workItemId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var changed = await db.IntakeRecoveryWorkItems
            .Where(x => x.TenantId == tenantId && x.Id == workItemId &&
                x.RecoveryStatus != IntakeRecoveryStatuses.Recovered &&
                x.RecoveryStatus != IntakeRecoveryStatuses.Cancelled)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RecoveryStatus, IntakeRecoveryStatuses.Cancelled)
                .SetProperty(x => x.Retryable, false)
                .SetProperty(x => x.ClaimToken, (string?)null)
                .SetProperty(x => x.CancelledAt, DateTimeOffset.UtcNow)
                .SetProperty(x => x.CancelledByUserId, actorUserId)
                .SetProperty(x => x.LastFailureCode, "RECOVERY_CANCELLED")
                .SetProperty(x => x.LastSafeMessage, "Automatic recovery was cancelled by an operator.")
                .SetProperty(x => x.FailureCategory, IntakeFailureCategories.Authorization)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                .SetProperty(x => x.Version, x => x.Version + 1), cancellationToken);
        return changed == 1;
    }

    public async Task<bool> MarkCreatingSnapshotFailedAsync(
        Guid tenantId,
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var changed = await db.ApprovedIntakeSnapshots
            .Where(x => x.TenantId == tenantId &&
                x.Id == snapshotId &&
                x.Status == ApprovedSnapshotStatuses.Creating)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, ApprovedSnapshotStatuses.Failed)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
        return changed == 1;
    }

    public async Task<Guid?> FindExecutionIdByDocumentAssociationItemAsync(
        Guid tenantId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentAssociationItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == itemId)
            .Select(x => (Guid?)x.ExecutionId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> FindAdapterSnapshotIdAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.IntakeAdapterExecutions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == executionId)
            .Select(x => (Guid?)x.SnapshotId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> FindDocumentAssociationSnapshotIdAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentAssociationExecutions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == executionId)
            .Select(x => (Guid?)x.SnapshotId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<IntakeRecoveryWorkItem> Items, long TotalCount)> ListAsync(
        Guid tenantId,
        RecoveryQuery query,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var page = Math.Clamp(query.Page, 1, 10_000);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var filtered = db.IntakeRecoveryWorkItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Where(x => query.Stage == null || x.Stage == query.Stage)
            .Where(x => query.Status == null || x.RecoveryStatus == query.Status)
            .Where(x => query.FailureCategory == null || x.FailureCategory == query.FailureCategory)
            .Where(x => query.Retryable == null || x.Retryable == query.Retryable)
            .Where(x => query.From == null || x.CreatedAt >= query.From)
            .Where(x => query.To == null || x.CreatedAt <= query.To);
        var total = await filtered.LongCountAsync(cancellationToken);
        var items = await filtered
            .OrderBy(x => x.StaleSince ?? x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<OperationsSummaryResponse> GetSummaryAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifacts = db.IntakeArtifacts.Where(x => x.TenantId == tenantId && x.CreatedAt >= from);
        var adapters = db.IntakeAdapterExecutions.Where(x => x.TenantId == tenantId && x.CreatedAt >= from);
        var associations = db.DocumentAssociationExecutions
            .Where(x => x.TenantId == tenantId && x.CreatedAt >= from);
        return new(
            await artifacts.LongCountAsync(cancellationToken),
            await artifacts.LongCountAsync(x => x.ProcessingStatus == "PROCESSING", cancellationToken),
            await db.IntakeReviews.LongCountAsync(x => x.TenantId == tenantId &&
                x.CreatedAt >= from && x.Status == IntakeReviewStatuses.Pending, cancellationToken),
            await artifacts.LongCountAsync(x =>
                x.ProcessingStatus == "COMPLETED" || x.ProcessingStatus == "SUCCEEDED",
                cancellationToken),
            await artifacts.LongCountAsync(x => x.FailureCode != null && !x.IsRetryable, cancellationToken),
            await artifacts.LongCountAsync(x => x.IsRetryable, cancellationToken),
            await db.IntakeRecoveryWorkItems.LongCountAsync(x => x.TenantId == tenantId &&
                x.StaleSince != null && x.RecoveryStatus != IntakeRecoveryStatuses.Recovered &&
                x.RecoveryStatus != IntakeRecoveryStatuses.Cancelled, cancellationToken),
            await db.IntakeRecoveryWorkItems.LongCountAsync(x => x.TenantId == tenantId &&
                x.RecoveryStatus == IntakeRecoveryStatuses.Recovered, cancellationToken),
            await adapters.LongCountAsync(x => x.Status == IntakeAdapterExecutionStatuses.Succeeded, cancellationToken),
            await adapters.LongCountAsync(x =>
                x.Status == IntakeAdapterExecutionStatuses.Failed ||
                x.Status == IntakeAdapterExecutionStatuses.Retryable,
                cancellationToken),
            await associations.LongCountAsync(x => x.Status == DocumentAssociationExecutionStatuses.Succeeded, cancellationToken),
            await associations.LongCountAsync(x =>
                x.Status == DocumentAssociationExecutionStatuses.Failed ||
                x.Status == DocumentAssociationExecutionStatuses.Retryable ||
                x.Status == DocumentAssociationExecutionStatuses.PartiallySucceeded,
                cancellationToken));
    }

    public async Task<IReadOnlyList<StageCountResponse>> GetStageFunnelAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifact = db.IntakeArtifacts.Where(x => x.TenantId == tenantId && x.CreatedAt >= from);
        var counts = new List<StageCountResponse>
        {
            new("Received", await artifact.LongCountAsync(cancellationToken)),
            new("Documented", await artifact.LongCountAsync(x => x.DocumentsServiceDocumentId != null, cancellationToken)),
            new("Classified", await db.ArtifactClassifications.LongCountAsync(x =>
                x.TenantId == tenantId && x.CreatedAt >= from && x.IsCurrent &&
                !StalledStatuses.Contains(x.Status), cancellationToken)),
            new("Extracted", await db.ArtifactExtractions.LongCountAsync(x =>
                x.TenantId == tenantId && x.CreatedAt >= from && x.IsCurrent &&
                !StalledStatuses.Contains(x.Status), cancellationToken)),
            new("Normalized", await db.ArtifactNormalizations.LongCountAsync(x =>
                x.TenantId == tenantId && x.CreatedAt >= from && x.IsCurrent &&
                !StalledStatuses.Contains(x.Status), cancellationToken)),
            new("Matched", await db.ArtifactMatchRuns.LongCountAsync(x =>
                x.TenantId == tenantId && x.CreatedAt >= from && x.IsCurrent &&
                !StalledStatuses.Contains(x.Status), cancellationToken)),
            new("Policy Evaluated", await db.ArtifactPolicyEvaluations.LongCountAsync(x =>
                x.TenantId == tenantId && x.CreatedAt >= from && x.IsCurrent &&
                !StalledStatuses.Contains(x.Status), cancellationToken)),
            new("Reviewed", await db.IntakeReviews.LongCountAsync(x =>
                x.TenantId == tenantId && x.CreatedAt >= from &&
                x.Status == IntakeReviewStatuses.Completed, cancellationToken)),
            new("Snapshot Ready", await db.ApprovedIntakeSnapshots.LongCountAsync(x =>
                x.TenantId == tenantId && x.CreatedAt >= from &&
                x.Status == ApprovedSnapshotStatuses.Ready, cancellationToken)),
            new("Routed", await db.IntakeAdapterExecutions.LongCountAsync(x =>
                x.TenantId == tenantId && x.CreatedAt >= from &&
                x.Status == IntakeAdapterExecutionStatuses.Succeeded, cancellationToken)),
            new("Documents Associated", await db.DocumentAssociationExecutions.LongCountAsync(x =>
                x.TenantId == tenantId && x.CreatedAt >= from &&
                x.Status == DocumentAssociationExecutionStatuses.Succeeded, cancellationToken)),
        };
        return counts;
    }

    public async Task<IReadOnlyList<FailureAggregateResponse>> GetFailuresAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.IntakeRecoveryWorkItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CreatedAt >= from &&
                x.LastFailureCode != null)
            .GroupBy(x => new
            {
                x.Stage,
                Category = x.FailureCategory ?? IntakeFailureCategories.Unknown,
                Code = x.LastFailureCode!,
                x.Retryable,
            })
            .OrderByDescending(x => x.Count())
            .Take(100)
            .Select(x => new FailureAggregateResponse(
                x.Key.Stage, x.Key.Category, x.Key.Code, x.Key.Retryable, x.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<RecoveryAnalyticsResponse> GetRecoveryAnalyticsAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = db.IntakeRecoveryWorkItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CreatedAt >= from);
        var attempts = await query.Select(x => (double)x.AttemptCount).ToListAsync(cancellationToken);
        var recent = await query.OrderByDescending(x => x.UpdatedAt).Take(25).ToListAsync(cancellationToken);
        return new(
            await query.LongCountAsync(x => x.RecoveryStatus == IntakeRecoveryStatuses.Stale, cancellationToken),
            await query.LongCountAsync(x => x.RecoveryStatus == IntakeRecoveryStatuses.Recovered, cancellationToken),
            await query.LongCountAsync(x => x.RecoveryStatus == IntakeRecoveryStatuses.Failed, cancellationToken),
            await query.LongCountAsync(x => x.RecoveryStatus == IntakeRecoveryStatuses.Exhausted, cancellationToken),
            attempts.Count == 0 ? 0 : attempts.Average(),
            recent.Select(Map).ToArray());
    }

    private static RecoveryWorkItemResponse Map(IntakeRecoveryWorkItem x) =>
        new(x.Id, x.TenantId, x.Stage, x.ObjectId, x.DomainStatus, x.RecoveryStatus,
            x.LastFailureCode, x.FailureCategory, x.LastSafeMessage, x.Retryable,
            x.AttemptCount, x.LastRecoveryAttemptAt, x.NextRetryAt, x.StaleSince,
            x.CreatedAt, x.CorrelationId);
}