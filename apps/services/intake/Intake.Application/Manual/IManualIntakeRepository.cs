using Intake.Domain.Manual;

namespace Intake.Application.Manual;

public interface IManualIntakeRepository
{
    Task<ManualIntakeSubmission?> FindAsync(
        Guid tenantId,
        Guid submissionId,
        CancellationToken cancellationToken);

    Task<ManualIntakeSubmission?> FindByClientRequestIdAsync(
        Guid tenantId,
        string clientRequestId,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ManualIntakeSubmission> Items, long TotalCount)> ListAsync(
        Guid tenantId,
        ManualIntakeListQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ManualIntakeSubmission>> ListAllAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task AddAsync(ManualIntakeSubmission submission, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}