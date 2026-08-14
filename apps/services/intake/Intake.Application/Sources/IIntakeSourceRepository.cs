using Intake.Domain.Sources;

namespace Intake.Application.Sources;

public interface IIntakeSourceRepository
{
    Task<IReadOnlyList<TenantIntakeSource>> ListTenantSourcesAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<TenantIntakeSource?> FindTenantSourceAsync(
        Guid tenantId,
        Guid sourceId,
        CancellationToken cancellationToken);

    Task<TenantIntakeSource?> FindByNormalizedEmailAddressAsync(
        string normalizedEmailAddress,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantIntakeSource>> ListTenantPurposeSourcesAsync(
        Guid tenantId,
        string purpose,
        CancellationToken cancellationToken);

    void Add(TenantIntakeSource source);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken);
}