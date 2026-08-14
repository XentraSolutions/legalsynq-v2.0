using Intake.Application.Matching;
using Intake.Domain.Matching;
using Microsoft.EntityFrameworkCore;

namespace Intake.Infrastructure.Persistence;

public sealed class EfArtifactMatchingRepository(IntakeDbContext db)
    : IArtifactMatchingRepository
{
    public Task<MatchingProfileDefinition?> FindProfileAsync(
        string code,
        int? version,
        CancellationToken cancellationToken)
    {
        var query = db.MatchingProfileDefinitions.AsNoTracking()
            .Where(profile => profile.Code == code);
        if (version.HasValue)
            query = query.Where(profile => profile.Version == version.Value);
        else
            query = query.Where(profile => profile.IsActive)
                .OrderByDescending(profile => profile.Version);
        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatchingProfileDefinition>> ListProfilesAsync(
        CancellationToken cancellationToken) =>
        await db.MatchingProfileDefinitions.AsNoTracking()
            .Where(profile => profile.IsActive)
            .OrderBy(profile => profile.Code)
            .ThenByDescending(profile => profile.Version)
            .ToListAsync(cancellationToken);

    public Task<ArtifactMatchRun?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        Guid normalizationId,
        CancellationToken cancellationToken) =>
        QueryRuns()
            .Where(run => run.TenantId == tenantId &&
                          run.IntakeArtifactId == artifactId &&
                          run.ArtifactNormalizationId == normalizationId &&
                          run.IsCurrent)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<ArtifactMatchRun?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken) =>
        QueryRuns()
            .Where(run => run.TenantId == tenantId &&
                          run.ExecutionKey == executionKey)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ArtifactMatchRun>> ListHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        await QueryRuns()
            .Where(run => run.TenantId == tenantId &&
                          run.IntakeArtifactId == artifactId)
            .OrderByDescending(run => run.CreatedAt)
            .ThenByDescending(run => run.Id)
            .ToListAsync(cancellationToken);

    public Task<ArtifactMatchRun?> FindBusinessDuplicateRunAsync(
        Guid tenantId,
        string businessKeyFingerprint,
        Guid excludedArtifactId,
        CancellationToken cancellationToken) =>
        QueryRuns()
            .Where(run => run.TenantId == tenantId &&
                          run.IntakeArtifactId != excludedArtifactId &&
                          run.BusinessKeyFingerprint == businessKeyFingerprint &&
                          (run.Status == MatchRunStatuses.Completed ||
                           run.Status == MatchRunStatuses.Partial))
            .OrderByDescending(run => run.CompletedAt)
            .ThenByDescending(run => run.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryAddMatchRunAsync(
        ArtifactMatchRun run,
        CancellationToken cancellationToken)
    {
        db.ArtifactMatchRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.Entry(run).State = EntityState.Detached;
            return false;
        }
    }

    public async Task FinalizeCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactMatchRun run,
        IReadOnlyList<ArtifactEntityMatch> entityMatches,
        IReadOnlyList<ArtifactMatchField> fields,
        IReadOnlyList<ArtifactDuplicateSignal> duplicateSignals,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.ArtifactMatchRuns
            .Where(item => item.TenantId == tenantId &&
                           item.IntakeArtifactId == artifactId &&
                           item.Id != run.Id &&
                           item.IsCurrent)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.IsCurrent, false)
                    .SetProperty(item => item.CurrentResultMarker, (string?)null),
                cancellationToken);

        run.IsCurrent = true;
        run.CurrentResultMarker = "CURRENT";
        db.ArtifactEntityMatches.AddRange(entityMatches);
        db.ArtifactMatchFields.AddRange(fields);
        db.ArtifactDuplicateSignals.AddRange(duplicateSignals);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    private IQueryable<ArtifactMatchRun> QueryRuns() =>
        db.ArtifactMatchRuns
            .AsNoTracking()
            .Include(run => run.EntityMatches)
            .ThenInclude(match => match.Fields)
            .Include(run => run.DuplicateSignals);
}