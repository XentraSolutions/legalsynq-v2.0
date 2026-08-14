using Intake.Application.Policy;
using Intake.Domain.Policy;
using Microsoft.EntityFrameworkCore;

namespace Intake.Infrastructure.Persistence;

public sealed class EfArtifactPolicyRepository(IntakeDbContext db)
    : IArtifactPolicyRepository
{
    public Task<PolicyProfileDefinition?> FindProfileAsync(
        string code,
        int? version,
        CancellationToken cancellationToken)
    {
        var query = db.PolicyProfileDefinitions.AsNoTracking()
            .Where(profile => profile.Code == code);
        if (version.HasValue)
            query = query.Where(profile => profile.Version == version.Value);
        else
            query = query.Where(profile => profile.IsActive)
                .OrderByDescending(profile => profile.Version);
        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PolicyProfileDefinition>> ListProfilesAsync(
        CancellationToken cancellationToken) =>
        await db.PolicyProfileDefinitions.AsNoTracking()
            .Where(profile => profile.IsActive)
            .OrderBy(profile => profile.Code)
            .ThenByDescending(profile => profile.Version)
            .ToListAsync(cancellationToken);

    public Task<ArtifactPolicyEvaluation?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        QueryEvaluations()
            .SingleOrDefaultAsync(
                evaluation => evaluation.TenantId == tenantId &&
                              evaluation.ArtifactId == artifactId &&
                              evaluation.IsCurrent,
                cancellationToken);

    public Task<ArtifactPolicyEvaluation?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken) =>
        QueryEvaluations()
            .SingleOrDefaultAsync(
                evaluation => evaluation.TenantId == tenantId &&
                              evaluation.ExecutionKey == executionKey,
                cancellationToken);

    public async Task<IReadOnlyList<ArtifactPolicyEvaluation>> ListHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        await QueryEvaluations()
            .Where(evaluation => evaluation.TenantId == tenantId &&
                                 evaluation.ArtifactId == artifactId)
            .OrderByDescending(evaluation => evaluation.CreatedAt)
            .ThenByDescending(evaluation => evaluation.Id)
            .ToListAsync(cancellationToken);

    public async Task<bool> TryAddEvaluationAsync(
        ArtifactPolicyEvaluation evaluation,
        CancellationToken cancellationToken)
    {
        db.ArtifactPolicyEvaluations.Add(evaluation);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.Entry(evaluation).State = EntityState.Detached;
            return false;
        }
    }

    public async Task FinalizeCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactPolicyEvaluation evaluation,
        IReadOnlyList<ArtifactPolicyFinding> findings,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);
        await db.ArtifactPolicyEvaluations
            .Where(item => item.TenantId == tenantId &&
                           item.ArtifactId == artifactId &&
                           item.Id != evaluation.Id &&
                           item.IsCurrent)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.IsCurrent, false)
                    .SetProperty(item => item.CurrentResultMarker, (string?)null),
                cancellationToken);
        evaluation.IsCurrent = true;
        evaluation.CurrentResultMarker = "CURRENT";
        evaluation.Findings = findings.ToList();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    private IQueryable<ArtifactPolicyEvaluation> QueryEvaluations() =>
        db.ArtifactPolicyEvaluations.AsNoTracking()
            .Include(evaluation => evaluation.Findings);
}