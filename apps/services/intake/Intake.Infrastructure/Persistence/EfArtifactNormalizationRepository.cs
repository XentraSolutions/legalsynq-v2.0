using Intake.Application.Normalization;
using Intake.Domain.Normalization;
using Microsoft.EntityFrameworkCore;

namespace Intake.Infrastructure.Persistence;

public sealed class EfArtifactNormalizationRepository(IntakeDbContext db)
    : IArtifactNormalizationRepository
{
    public Task<NormalizationProfileDefinition?> FindProfileAsync(
        string code,
        int? version,
        CancellationToken cancellationToken)
    {
        var query = db.NormalizationProfileDefinitions.AsNoTracking()
            .Where(profile => profile.Code == code);
        if (version.HasValue)
            query = query.Where(profile => profile.Version == version.Value);
        else
            query = query.Where(profile => profile.IsActive)
                .OrderByDescending(profile => profile.Version);
        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NormalizationProfileDefinition>> ListProfilesAsync(
        CancellationToken cancellationToken) =>
        await db.NormalizationProfileDefinitions.AsNoTracking()
            .Where(profile => profile.IsActive)
            .OrderBy(profile => profile.Code)
            .ThenByDescending(profile => profile.Version)
            .ToListAsync(cancellationToken);

    public Task<ArtifactNormalization?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        Guid artifactExtractionId,
        CancellationToken cancellationToken) =>
        db.ArtifactNormalizations.AsNoTracking()
            .Include(normalization => normalization.Facts)
            .SingleOrDefaultAsync(
                normalization => normalization.TenantId == tenantId &&
                                  normalization.IntakeArtifactId == artifactId &&
                                  normalization.ArtifactExtractionId == artifactExtractionId &&
                                  normalization.IsCurrent,
                cancellationToken);

    public Task<ArtifactNormalization?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken) =>
        db.ArtifactNormalizations.AsNoTracking()
            .Include(normalization => normalization.Facts)
            .SingleOrDefaultAsync(
                normalization => normalization.TenantId == tenantId &&
                                  normalization.ExecutionKey == executionKey,
                cancellationToken);

    public async Task<IReadOnlyList<ArtifactNormalization>> ListHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        await db.ArtifactNormalizations.AsNoTracking()
            .Include(normalization => normalization.Facts)
            .Where(normalization => normalization.TenantId == tenantId &&
                                    normalization.IntakeArtifactId == artifactId)
            .OrderByDescending(normalization => normalization.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> TryAddNormalizationAsync(
        ArtifactNormalization normalization,
        CancellationToken cancellationToken)
    {
        db.ArtifactNormalizations.Add(normalization);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.Entry(normalization).State = EntityState.Detached;
            return false;
        }
    }

    public async Task FinalizeCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactNormalization normalization,
        IReadOnlyList<ArtifactNormalizedFact> facts,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.ArtifactNormalizations
            .Where(item => item.TenantId == tenantId &&
                           item.IntakeArtifactId == artifactId &&
                           item.Id != normalization.Id &&
                           item.IsCurrent)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.IsCurrent, false)
                    .SetProperty(item => item.CurrentResultMarker, (string?)null),
                cancellationToken);
        normalization.IsCurrent = true;
        normalization.CurrentResultMarker = "CURRENT";
        normalization.Facts = facts.ToList();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}