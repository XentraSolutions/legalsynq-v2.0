using Intake.Application.Classification;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Microsoft.EntityFrameworkCore;

namespace Intake.Infrastructure.Persistence;

public sealed class EfClassificationRepository(IntakeDbContext db) : IClassificationRepository
{
    public Task<TenantAiPolicy?> FindPolicyAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        db.TenantAiPolicies.SingleOrDefaultAsync(
            policy => policy.TenantId == tenantId,
            cancellationToken);

    public async Task SavePolicyAsync(
        TenantAiPolicy policy,
        CancellationToken cancellationToken)
    {
        if (db.Entry(policy).State == EntityState.Detached)
            db.TenantAiPolicies.Update(policy);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<ClassificationProfileDefinition?> FindProfileAsync(
        string code,
        int? version,
        CancellationToken cancellationToken)
    {
        var query = db.ClassificationProfileDefinitions.AsNoTracking()
            .Where(profile => profile.Code == code);
        if (version.HasValue)
            query = query.Where(profile => profile.Version == version.Value);
        else
            query = query.Where(profile => profile.IsActive)
                .OrderByDescending(profile => profile.Version);
        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ClassificationTaxonomyDefinition?> FindTaxonomyAsync(
        string code,
        int version,
        CancellationToken cancellationToken) =>
        db.ClassificationTaxonomyDefinitions.AsNoTracking().SingleOrDefaultAsync(
            taxonomy => taxonomy.Code == code && taxonomy.Version == version,
            cancellationToken);

    public Task<ClassificationPromptDefinition?> FindPromptAsync(
        string code,
        int version,
        CancellationToken cancellationToken) =>
        db.ClassificationPromptDefinitions.AsNoTracking().SingleOrDefaultAsync(
            prompt => prompt.Code == code && prompt.Version == version,
            cancellationToken);

    public async Task<IReadOnlyList<ClassificationProfileDefinition>> ListProfilesAsync(
        CancellationToken cancellationToken) =>
        await db.ClassificationProfileDefinitions.AsNoTracking()
            .Where(profile => profile.IsActive)
            .OrderBy(profile => profile.Code)
            .ThenByDescending(profile => profile.Version)
            .ToListAsync(cancellationToken);

    public Task<IntakeArtifact?> FindArtifactAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        db.IntakeArtifacts.AsNoTracking().SingleOrDefaultAsync(
            artifact => artifact.TenantId == tenantId && artifact.Id == artifactId,
            cancellationToken);

    public Task<ArtifactClassification?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        db.ArtifactClassifications.AsNoTracking().SingleOrDefaultAsync(
            classification => classification.TenantId == tenantId &&
                               classification.IntakeArtifactId == artifactId &&
                               classification.IsCurrent,
            cancellationToken);

    public Task<ArtifactClassification?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken) =>
        db.ArtifactClassifications.AsNoTracking().SingleOrDefaultAsync(
            classification => classification.TenantId == tenantId &&
                               classification.ExecutionKey == executionKey,
            cancellationToken);

    public async Task<IReadOnlyList<ArtifactClassification>> ListHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        await db.ArtifactClassifications.AsNoTracking()
            .Where(classification => classification.TenantId == tenantId &&
                                     classification.IntakeArtifactId == artifactId)
            .OrderByDescending(classification => classification.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> TryClaimAsync(
        Guid tenantId,
        Guid classificationId,
        bool retryFailed,
        CancellationToken cancellationToken)
    {
        var statuses = retryFailed
            ? new[] { ClassificationStatuses.Pending, ClassificationStatuses.Failed }
            : new[] { ClassificationStatuses.Pending };
        var updated = await db.ArtifactClassifications
            .Where(classification => classification.TenantId == tenantId &&
                                     classification.Id == classificationId &&
                                     statuses.Contains(classification.Status) &&
                                     (!retryFailed ||
                                      classification.Status != ClassificationStatuses.Failed ||
                                      classification.IsRetryable))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(classification => classification.Status, ClassificationStatuses.Processing)
                .SetProperty(classification => classification.AttemptCount, classification => classification.AttemptCount + 1)
                .SetProperty(classification => classification.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
        return updated == 1;
    }

    public Task ClearCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        Guid replacementClassificationId,
        CancellationToken cancellationToken) =>
        db.ArtifactClassifications
            .Where(classification => classification.TenantId == tenantId &&
                                     classification.IntakeArtifactId == artifactId &&
                                     classification.IsCurrent &&
                                     classification.Id != replacementClassificationId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(classification => classification.IsCurrent, false)
                    .SetProperty(classification => classification.CurrentResultMarker, (string?)null),
                cancellationToken);

    public async Task FinalizeCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactClassification classification,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.ArtifactClassifications
            .Where(item => item.TenantId == tenantId &&
                           item.IntakeArtifactId == artifactId &&
                           item.Id != classification.Id &&
                           item.IsCurrent)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.IsCurrent, false)
                    .SetProperty(item => item.CurrentResultMarker, (string?)null),
                cancellationToken);

        classification.IsCurrent = true;
        classification.CurrentResultMarker = "CURRENT";
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> TryAddClassificationAsync(
        ArtifactClassification classification,
        CancellationToken cancellationToken)
    {
        db.ArtifactClassifications.Add(classification);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.Entry(classification).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}