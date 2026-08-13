using Tenant.Domain;

namespace Tenant.Application.Interfaces;

public interface ITenantRegistrationRepository
{
    Task<TenantRegistration?> GetAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasPendingConflictAsync(string code, string email, CancellationToken ct = default);
    Task<(List<TenantRegistration> Items, int Total)> ListAsync(string? registrationStatus,
        string? provisioningStatus, string? search, DateTime? submittedFrom, DateTime? submittedTo,
        int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(TenantRegistration registration, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

