using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using BuildingBlocks.Exceptions;

namespace Liens.Infrastructure.Repositories;

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly LiensDbContext _db;

    public CompanyRepository(LiensDbContext db) => _db = db;

    public Task<List<CompanyType>> GetCompanyTypesAsync(CancellationToken ct = default)
        => _db.CompanyTypes.AsNoTracking()
            .Where(value => value.IsActive)
            .OrderBy(value => value.SortOrder)
            .ThenBy(value => value.Name)
            .ToListAsync(ct);

    public Task<CompanyType?> GetCompanyTypeAsync(Guid id, CancellationToken ct = default)
        => _db.CompanyTypes.AsNoTracking().FirstOrDefaultAsync(value => value.Id == id, ct);

    public Task<List<ContactPersonType>> GetContactPersonTypesAsync(
        Guid companyTypeId, CancellationToken ct = default)
        => _db.ContactPersonTypes.AsNoTracking()
            .Where(value => value.CompanyTypeId == companyTypeId && value.IsActive)
            .OrderBy(value => value.SortOrder)
            .ThenBy(value => value.Name)
            .ToListAsync(ct);

    public Task<ContactPersonType?> GetContactPersonTypeAsync(Guid id, CancellationToken ct = default)
        => _db.ContactPersonTypes.AsNoTracking().FirstOrDefaultAsync(value => value.Id == id, ct);

    public async Task<(List<Company> Items, int TotalCount)> SearchCompaniesAsync(
        Guid tenantId, Guid orgId, string? search, Guid? companyTypeId, bool? isActive,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Companies.AsNoTracking()
            .Include(value => value.CompanyType)
            .Where(value => value.TenantId == tenantId && value.OrgId == orgId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(value => value.Name.Contains(term) ||
                (value.Email != null && value.Email.Contains(term)) ||
                (value.City != null && value.City.Contains(term)));
        }
        if (companyTypeId.HasValue) query = query.Where(value => value.CompanyTypeId == companyTypeId.Value);
        if (isActive.HasValue) query = query.Where(value => value.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderBy(value => value.Name)
            .ThenBy(value => value.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, totalCount);
    }

    public Task<Company?> GetCompanyAsync(
        Guid tenantId, Guid orgId, Guid id, CancellationToken ct = default)
        => _db.Companies.Include(value => value.CompanyType)
            .FirstOrDefaultAsync(value => value.TenantId == tenantId && value.OrgId == orgId && value.Id == id, ct);

    public Task<bool> CompanyNameExistsAsync(
        Guid tenantId, Guid orgId, Guid companyTypeId, string normalizedName,
        Guid? excludingId = null, CancellationToken ct = default)
        => _db.Companies.AsNoTracking().AnyAsync(value =>
            value.TenantId == tenantId &&
            value.OrgId == orgId &&
            value.CompanyTypeId == companyTypeId &&
            value.NormalizedName == normalizedName &&
            (!excludingId.HasValue || value.Id != excludingId.Value), ct);

    public async Task AddCompanyAsync(Company company, CancellationToken ct = default)
    {
        await _db.Companies.AddAsync(company, ct);
        await SaveCompanyChangesAsync(ct);
    }

    public Task UpdateCompanyAsync(Company company, CancellationToken ct = default)
        => SaveCompanyChangesAsync(ct);

    public Task<List<CompanyContactPerson>> GetContactPersonsAsync(
        Guid tenantId, Guid companyId, bool? isActive, CancellationToken ct = default)
    {
        var query = _db.CompanyContactPersons.AsNoTracking()
            .Include(value => value.ContactPersonType)
            .Where(value => value.TenantId == tenantId && value.CompanyId == companyId);
        if (isActive.HasValue) query = query.Where(value => value.IsActive == isActive.Value);
        return query.OrderBy(value => value.LastName).ThenBy(value => value.FirstName).ToListAsync(ct);
    }

    public Task<CompanyContactPerson?> GetContactPersonAsync(
        Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default)
        => _db.CompanyContactPersons.Include(value => value.ContactPersonType)
            .FirstOrDefaultAsync(value => value.TenantId == tenantId && value.CompanyId == companyId && value.Id == id, ct);

    public async Task AddContactPersonAsync(CompanyContactPerson contact, CancellationToken ct = default)
    {
        await _db.CompanyContactPersons.AddAsync(contact, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task UpdateContactPersonAsync(CompanyContactPerson contact, CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    private async Task SaveCompanyChangesAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            throw new ConflictException("A company with this name and type already exists.");
        }
    }
}
