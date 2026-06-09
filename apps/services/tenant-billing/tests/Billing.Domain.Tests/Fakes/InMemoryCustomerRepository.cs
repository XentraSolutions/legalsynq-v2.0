using Billing.Domain.Entities;
using Billing.Domain.Repositories;

namespace Billing.Domain.Tests.Fakes;

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

    public Task<IReadOnlyList<Customer>> GetByExternalReferenceAsync(
        Guid tenantId,
        string externalReference,
        int limit = 2,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            return Task.FromResult<IReadOnlyList<Customer>>(Array.Empty<Customer>());
        }

        var effectiveLimit = limit < 1 ? 1 : (limit > 10 ? 10 : limit);
        var needle = externalReference.Trim();

        var matches = _customers.Values
            .Where(c => c.TenantId == tenantId
                        && !c.IsDeleted
                        && c.ExternalReference != null
                        && string.Equals(c.ExternalReference, needle, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.CreatedAt)
            .Take(effectiveLimit)
            .ToList();
        return Task.FromResult<IReadOnlyList<Customer>>(matches);
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
