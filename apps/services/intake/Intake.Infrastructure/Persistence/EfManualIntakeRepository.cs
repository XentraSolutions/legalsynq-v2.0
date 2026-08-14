using Intake.Application.Manual;
using Intake.Domain.Manual;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Intake.Infrastructure.Persistence;

public sealed class EfManualIntakeRepository(IntakeDbContext db) : IManualIntakeRepository
{
    public Task<ManualIntakeSubmission?> FindAsync(
        Guid tenantId,
        Guid submissionId,
        CancellationToken cancellationToken) =>
        db.ManualIntakeSubmissions.AsNoTracking().SingleOrDefaultAsync(
            submission => submission.TenantId == tenantId && submission.Id == submissionId,
            cancellationToken);

    public Task<ManualIntakeSubmission?> FindByClientRequestIdAsync(
        Guid tenantId,
        string clientRequestId,
        CancellationToken cancellationToken) =>
        db.ManualIntakeSubmissions.AsNoTracking().SingleOrDefaultAsync(
            submission => submission.TenantId == tenantId &&
                          submission.ClientRequestId == clientRequestId,
            cancellationToken);

    public async Task<(IReadOnlyList<ManualIntakeSubmission> Items, long TotalCount)> ListAsync(
        Guid tenantId,
        ManualIntakeListQuery query,
        CancellationToken cancellationToken)
    {
        var filtered = db.ManualIntakeSubmissions.AsNoTracking()
            .Where(submission => submission.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(query.Status))
            filtered = filtered.Where(submission => submission.Status == query.Status.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(query.Purpose))
            filtered = filtered.Where(submission => submission.Purpose == query.Purpose.Trim().ToUpperInvariant());

        var total = await filtered.LongCountAsync(cancellationToken);
        var page = Math.Clamp(query.Page, 1, 10_000);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var items = await filtered
            .OrderByDescending(submission => submission.CreatedAt)
            .ThenByDescending(submission => submission.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<ManualIntakeSubmission>> ListAllAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await db.ManualIntakeSubmissions
            .AsNoTracking()
            .Where(submission => submission.TenantId == tenantId)
            .OrderByDescending(submission => submission.CreatedAt)
            .ThenByDescending(submission => submission.Id)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        ManualIntakeSubmission submission,
        CancellationToken cancellationToken)
    {
        db.ManualIntakeSubmissions.Add(submission);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
        {
            throw new InvalidOperationException("A manual Intake submission already uses this idempotency key.", exception);
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}