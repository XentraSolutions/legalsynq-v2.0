using Intake.Application.Extraction;
using Intake.Domain.Extraction;
using Microsoft.EntityFrameworkCore;

namespace Intake.Infrastructure.Persistence;

public sealed class EfArtifactExtractionRepository(IntakeDbContext db)
    : IArtifactExtractionRepository
{
    public Task<ExtractionProfileDefinition?> FindProfileAsync(
        string code,
        int? version,
        CancellationToken cancellationToken)
    {
        var query = db.ExtractionProfileDefinitions.AsNoTracking()
            .Where(profile => profile.Code == code);
        if (version.HasValue)
            query = query.Where(profile => profile.Version == version.Value);
        else
            query = query.Where(profile => profile.IsActive)
                .OrderByDescending(profile => profile.Version);
        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ExtractionSchemaDefinition?> FindSchemaAsync(
        string code,
        int version,
        string classificationCode,
        CancellationToken cancellationToken) =>
        db.ExtractionSchemaDefinitions.AsNoTracking().SingleOrDefaultAsync(
            schema => schema.Code == code &&
                      schema.Version == version &&
                      schema.ClassificationCode == classificationCode,
            cancellationToken);

    public Task<ExtractionPromptDefinition?> FindPromptAsync(
        string code,
        int version,
        string classificationCode,
        CancellationToken cancellationToken) =>
        db.ExtractionPromptDefinitions.AsNoTracking().SingleOrDefaultAsync(
            prompt => prompt.Code == code &&
                      prompt.Version == version &&
                      prompt.ClassificationCode == classificationCode,
            cancellationToken);

    public async Task<IReadOnlyList<ExtractionProfileDefinition>> ListProfilesAsync(
        CancellationToken cancellationToken) =>
        await db.ExtractionProfileDefinitions.AsNoTracking()
            .Where(profile => profile.IsActive)
            .OrderBy(profile => profile.Code)
            .ThenByDescending(profile => profile.Version)
            .ToListAsync(cancellationToken);

    public Task<ArtifactExtraction?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        Guid classificationId,
        CancellationToken cancellationToken) =>
        db.ArtifactExtractions.AsNoTracking()
            .Include(extraction => extraction.Facts)
            .SingleOrDefaultAsync(
                extraction => extraction.TenantId == tenantId &&
                              extraction.IntakeArtifactId == artifactId &&
                              extraction.ClassificationId == classificationId &&
                              extraction.IsCurrent,
                cancellationToken);

    public Task<ArtifactExtraction?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken) =>
        db.ArtifactExtractions.AsNoTracking()
            .Include(extraction => extraction.Facts)
            .SingleOrDefaultAsync(
                extraction => extraction.TenantId == tenantId &&
                              extraction.ExecutionKey == executionKey,
                cancellationToken);

    public async Task<IReadOnlyList<ArtifactExtraction>> ListHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        await db.ArtifactExtractions.AsNoTracking()
            .Include(extraction => extraction.Facts)
            .Where(extraction => extraction.TenantId == tenantId &&
                                 extraction.IntakeArtifactId == artifactId)
            .OrderByDescending(extraction => extraction.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> TryClaimAsync(
        Guid tenantId,
        Guid extractionId,
        bool retryFailed,
        CancellationToken cancellationToken)
    {
        var statuses = retryFailed
            ? new[] { ExtractionStatuses.Pending, ExtractionStatuses.Failed }
            : new[] { ExtractionStatuses.Pending };
        var updated = await db.ArtifactExtractions
            .Where(extraction => extraction.TenantId == tenantId &&
                                extraction.Id == extractionId &&
                                statuses.Contains(extraction.Status) &&
                                (!retryFailed ||
                                 extraction.Status != ExtractionStatuses.Failed ||
                                 extraction.IsRetryable))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(extraction => extraction.Status, ExtractionStatuses.Processing)
                .SetProperty(extraction => extraction.AttemptCount, extraction => extraction.AttemptCount + 1)
                .SetProperty(extraction => extraction.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
        return updated == 1;
    }

    public async Task FinalizeCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactExtraction extraction,
        IReadOnlyList<ArtifactExtractedFact> facts,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.ArtifactExtractions
            .Where(item => item.TenantId == tenantId &&
                           item.IntakeArtifactId == artifactId &&
                           item.Id != extraction.Id &&
                           item.IsCurrent)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.IsCurrent, false)
                    .SetProperty(item => item.CurrentResultMarker, (string?)null),
                cancellationToken);
        extraction.IsCurrent = true;
        extraction.CurrentResultMarker = "CURRENT";
        extraction.Facts = facts.ToList();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> TryAddExtractionAsync(
        ArtifactExtraction extraction,
        CancellationToken cancellationToken)
    {
        db.ArtifactExtractions.Add(extraction);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.Entry(extraction).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}