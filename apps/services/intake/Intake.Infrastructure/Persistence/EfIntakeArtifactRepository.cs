using Intake.Application.Artifacts;
using Intake.Domain.Artifacts;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Intake.Infrastructure.Persistence;

public sealed class EfIntakeArtifactRepository(IntakeDbContext db) : IIntakeArtifactRepository
{
    public async Task<IReadOnlyList<IntakeArtifact>> ListByEmailAsync(
        Guid tenantId,
        Guid emailId,
        CancellationToken cancellationToken)
    {
        return await db.IntakeArtifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == tenantId && artifact.InboundEmailId == emailId)
            .OrderBy(artifact => artifact.ArtifactOrdinal)
            .ThenBy(artifact => artifact.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IntakeArtifact>> ListByManualSubmissionAsync(
        Guid tenantId,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        return await db.IntakeArtifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == tenantId &&
                               artifact.ManualIntakeSubmissionId == submissionId)
            .OrderBy(artifact => artifact.ArtifactOrdinal)
            .ThenBy(artifact => artifact.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<IntakeArtifact?> FindByManualKeyAsync(
        Guid tenantId,
        Guid submissionId,
        string artifactKey,
        CancellationToken cancellationToken) =>
        db.IntakeArtifacts.SingleOrDefaultAsync(
            artifact => artifact.TenantId == tenantId &&
                        artifact.ManualIntakeSubmissionId == submissionId &&
                        artifact.ArtifactKey == artifactKey,
            cancellationToken);

    public Task<IntakeArtifact?> FindAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        db.IntakeArtifacts.SingleOrDefaultAsync(
            artifact => artifact.TenantId == tenantId && artifact.Id == artifactId,
            cancellationToken);

    public Task<IntakeArtifact?> FindByKeyAsync(
        Guid tenantId,
        Guid emailId,
        string artifactKey,
        CancellationToken cancellationToken) =>
        db.IntakeArtifacts.SingleOrDefaultAsync(
            artifact => artifact.TenantId == tenantId &&
                        artifact.InboundEmailId == emailId &&
                        artifact.ArtifactKey == artifactKey,
            cancellationToken);

    public async Task<IReadOnlyList<IntakeArtifact>> ListBySha256Async(
        Guid tenantId,
        string sha256,
        Guid excludedArtifactId,
        CancellationToken cancellationToken) =>
        await db.IntakeArtifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == tenantId &&
                               artifact.Id != excludedArtifactId &&
                               artifact.Sha256 != null &&
                               artifact.Sha256 == sha256)
            .OrderBy(artifact => artifact.CreatedAt)
            .ThenBy(artifact => artifact.Id)
            .ToListAsync(cancellationToken);

    public async Task<IntakeArtifact> AddOrGetAsync(
        IntakeArtifact artifact,
        CancellationToken cancellationToken)
    {
        db.IntakeArtifacts.Add(artifact);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return artifact;
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            db.ChangeTracker.Clear();
            return await db.IntakeArtifacts.SingleAsync(
                existing => existing.TenantId == artifact.TenantId &&
                            existing.InboundEmailId == artifact.InboundEmailId &&
                            existing.ManualIntakeSubmissionId == artifact.ManualIntakeSubmissionId &&
                            existing.ArtifactKey == artifact.ArtifactKey,
                cancellationToken);
        }
    }

    public async Task<bool> TryClaimAsync(
        Guid tenantId,
        Guid artifactId,
        bool retryFailed,
        CancellationToken cancellationToken)
    {
        var claimableStatuses = retryFailed
            ? new[] { IntakeArtifactProcessingStatuses.Pending, IntakeArtifactProcessingStatuses.Failed }
            : new[] { IntakeArtifactProcessingStatuses.Pending };

        var now = DateTimeOffset.UtcNow;
        var updated = await db.IntakeArtifacts
            .Where(artifact => artifact.TenantId == tenantId &&
                               artifact.Id == artifactId &&
                               claimableStatuses.Contains(artifact.ProcessingStatus) &&
                               (!retryFailed ||
                                artifact.ProcessingStatus != IntakeArtifactProcessingStatuses.Failed ||
                                artifact.IsRetryable))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(artifact => artifact.ProcessingStatus, IntakeArtifactProcessingStatuses.Processing)
                .SetProperty(artifact => artifact.AttemptCount, artifact => artifact.AttemptCount + 1)
                .SetProperty(artifact => artifact.FailureCode, (string?)null)
                .SetProperty(artifact => artifact.FailureMessage, (string?)null)
                .SetProperty(artifact => artifact.UpdatedAt, now),
                cancellationToken);

        return updated == 1;
    }

    public Task SaveAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task UpdateManualSubmissionStatusAsync(
        Guid tenantId,
        Guid submissionId,
        string status,
        string? failureMessage,
        DateTimeOffset? completedAt,
        CancellationToken cancellationToken)
    {
        await db.ManualIntakeSubmissions
            .Where(submission => submission.TenantId == tenantId && submission.Id == submissionId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(submission => submission.Status, status)
                .SetProperty(submission => submission.FailureMessage, failureMessage)
                .SetProperty(submission => submission.CompletedAt, completedAt)
                .SetProperty(submission => submission.Version, submission => submission.Version + 1)
                .SetProperty(submission => submission.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }

    public async Task UpdateEmailProcessingStatusAsync(
        Guid tenantId,
        Guid emailId,
        string status,
        CancellationToken cancellationToken)
    {
        await db.InboundEmails
            .Where(email => email.TenantId == tenantId && email.Id == emailId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(email => email.ProcessingStatus, status)
                .SetProperty(email => email.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }

    public async Task<IntakeArtifactAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        Guid? emailId,
        CancellationToken cancellationToken)
    {
        var query = db.IntakeArtifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == tenantId);
        if (emailId.HasValue)
            query = query.Where(artifact => artifact.InboundEmailId == emailId.Value);

        var aggregate = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.LongCount(),
                Completed = group.Sum(artifact =>
                    artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed ? 1L : 0L),
                Failed = group.Sum(artifact =>
                    artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Failed ? 1L : 0L),
                Skipped = group.Sum(artifact =>
                    artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Skipped ? 1L : 0L),
                Pending = group.Sum(artifact =>
                    artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Pending ? 1L : 0L),
                Processing = group.Sum(artifact =>
                    artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Processing ? 1L : 0L),
                TotalBytes = group.Sum(artifact => (long?)artifact.SizeBytes) ?? 0L,
                UploadedBytes = group
                    .Where(artifact =>
                        artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed)
                    .Sum(artifact => (long?)artifact.SizeBytes) ?? 0L,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new(
            tenantId,
            emailId,
            aggregate?.Total ?? 0,
            aggregate?.Completed ?? 0,
            aggregate?.Failed ?? 0,
            aggregate?.Skipped ?? 0,
            aggregate?.Pending ?? 0,
            aggregate?.Processing ?? 0,
            aggregate?.TotalBytes ?? 0,
            aggregate?.UploadedBytes ?? 0);
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };
}