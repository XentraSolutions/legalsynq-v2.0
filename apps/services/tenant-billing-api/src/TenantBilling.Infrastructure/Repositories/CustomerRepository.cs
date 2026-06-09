using Microsoft.EntityFrameworkCore;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Repositories;
using TenantBilling.Infrastructure.Data;

namespace TenantBilling.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private const int MinPageSize = 1;
    private const int MaxPageSize = 100;

    private readonly TenantBillingDbContext _db;

    public CustomerRepository(TenantBillingDbContext db) => _db = db;

    public async Task<Customer> AddAsync(Customer customer, CancellationToken ct = default)
    {
        await _db.Customers.AddAsync(customer, ct);
        await _db.SaveChangesAsync(ct);
        return customer;
    }

    public async Task<Customer> UpdateAsync(Customer customer, CancellationToken ct = default)
    {
        _db.Customers.Update(customer);
        await _db.SaveChangesAsync(ct);
        return customer;
    }

    public Task<Customer?> GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
        => _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == customerId, ct);

    public Task<Customer?> GetActiveByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
        => _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == customerId && !c.IsDeleted, ct);

    public async Task<IReadOnlyList<Customer>> ListAsync(
        Guid tenantId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (effectivePage, effectivePageSize) = NormalizePaging(page, pageSize);
        var query = BaseQuery(tenantId, search);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(Guid tenantId, string? search, CancellationToken ct = default)
        => BaseQuery(tenantId, search).CountAsync(ct);

    public Task<bool> ExistsByTenantAndEmailAsync(
        Guid tenantId,
        string email,
        Guid? excludingCustomerId = null,
        CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var query = _db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId
                        && !c.IsDeleted
                        && c.Email.ToLower() == normalized);

        if (excludingCustomerId.HasValue)
        {
            var excluded = excludingCustomerId.Value;
            query = query.Where(c => c.Id != excluded);
        }

        return query.AnyAsync(ct);
    }

    private IQueryable<Customer> BaseQuery(Guid tenantId, string? search)
    {
        var query = _db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted);

        if (string.IsNullOrWhiteSpace(search)) return query;

        var needle = search.Trim().ToLower();
        return query.Where(c =>
            c.Name.ToLower().Contains(needle)
            || c.Email.ToLower().Contains(needle)
            || (c.Phone != null && c.Phone.ToLower().Contains(needle))
            || (c.ExternalReference != null && c.ExternalReference.ToLower().Contains(needle)));
    }

    private static (int page, int pageSize) NormalizePaging(int page, int pageSize)
    {
        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize < MinPageSize
            ? 25
            : (pageSize > MaxPageSize ? MaxPageSize : pageSize);
        return (effectivePage, effectivePageSize);
    }
}
