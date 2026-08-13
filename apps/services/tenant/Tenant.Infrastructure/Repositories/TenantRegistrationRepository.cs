using Microsoft.EntityFrameworkCore;
using Tenant.Application.Interfaces;
using Tenant.Domain;
using Tenant.Infrastructure.Data;

namespace Tenant.Infrastructure.Repositories;

public sealed class TenantRegistrationRepository(TenantDbContext db) : ITenantRegistrationRepository
{
    public Task<TenantRegistration?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.TenantRegistrations.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> HasPendingConflictAsync(string code, string email, CancellationToken ct = default) =>
        db.TenantRegistrations.AnyAsync(x => x.RegistrationStatus == RegistrationStatus.PendingReview &&
            (x.TenantCode == code || x.AdminEmail == email), ct);

    public async Task<(List<TenantRegistration> Items, int Total)> ListAsync(string? registrationStatus,
        string? provisioningStatus, string? search, DateTime? submittedFrom, DateTime? submittedTo,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.TenantRegistrations.AsNoTracking().AsQueryable();
        if (Enum.TryParse<RegistrationStatus>(registrationStatus, true, out var rs)) query = query.Where(x => x.RegistrationStatus == rs);
        if (Enum.TryParse<RegistrationProvisioningStatus>(provisioningStatus, true, out var ps)) query = query.Where(x => x.ProvisioningStatus == ps);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            var organizationTerm = term.Replace(" ", "_");
            query = query.Where(x => x.TenantName.ToLower().Contains(term) || x.TenantCode.Contains(term) ||
                x.OrganizationType.ToLower().Contains(term) || x.OrganizationType.ToLower().Contains(organizationTerm) ||
                x.AdminFirstName.ToLower().Contains(term) || x.AdminLastName.ToLower().Contains(term) || x.AdminEmail.Contains(term));
        }
        if (submittedFrom is not null) query = query.Where(x => x.CreatedAtUtc >= submittedFrom);
        if (submittedTo is not null) query = query.Where(x => x.CreatedAtUtc <= submittedTo);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task AddAsync(TenantRegistration registration, CancellationToken ct = default)
    { db.TenantRegistrations.Add(registration); await db.SaveChangesAsync(ct); }
    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
