using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Repositories;

namespace TenantBilling.Domain.Tests.Fakes;

internal sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<Guid, Customer> _customers = new();

    public Task<Customer> AddAsync(Customer customer, CancellationToken ct = default)
    {
        _customers[customer.Id] = customer;
        return Task.FromResult(customer);
    }

    public Task<Customer> UpdateAsync(Customer customer, CancellationToken ct = default)
    {
        _customers[customer.Id] = customer;
        return Task.FromResult(customer);
    }

    public Task<Customer?> GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
        => Task.FromResult(
            _customers.TryGetValue(customerId, out var c) && c.TenantId == tenantId ? c : null);

    public Task<Customer?> GetActiveByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
        => Task.FromResult(
            _customers.TryGetValue(customerId, out var c) && c.TenantId == tenantId && !c.IsDeleted
                ? c
                : null);

    public Task<IReadOnlyList<Customer>> ListAsync(
        Guid tenantId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = Filter(tenantId, search)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return Task.FromResult<IReadOnlyList<Customer>>(query);
    }

    public Task<int> CountAsync(Guid tenantId, string? search, CancellationToken ct = default)
        => Task.FromResult(Filter(tenantId, search).Count());

    public Task<bool> ExistsByTenantAndEmailAsync(
        Guid tenantId,
        string email,
        Guid? excludingCustomerId = null,
        CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var hit = _customers.Values.Any(c =>
            c.TenantId == tenantId
            && !c.IsDeleted
            && string.Equals(c.Email, normalized, StringComparison.OrdinalIgnoreCase)
            && (!excludingCustomerId.HasValue || c.Id != excludingCustomerId.Value));
        return Task.FromResult(hit);
    }

    private IEnumerable<Customer> Filter(Guid tenantId, string? search)
    {
        var rows = _customers.Values.Where(c => c.TenantId == tenantId && !c.IsDeleted);
        if (string.IsNullOrWhiteSpace(search)) return rows;

        var needle = search.Trim().ToLowerInvariant();
        return rows.Where(c =>
            c.Name.ToLowerInvariant().Contains(needle)
            || c.Email.ToLowerInvariant().Contains(needle)
            || (c.Phone != null && c.Phone.ToLowerInvariant().Contains(needle))
            || (c.ExternalReference != null && c.ExternalReference.ToLowerInvariant().Contains(needle)));
    }
}
