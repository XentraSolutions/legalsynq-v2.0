using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ICompanyRepository
{
    Task<List<CompanyType>> GetCompanyTypesAsync(CancellationToken ct = default);
    Task<CompanyType?> GetCompanyTypeAsync(Guid id, CancellationToken ct = default);
    Task<List<ContactPersonType>> GetContactPersonTypesAsync(Guid companyTypeId, CancellationToken ct = default);
    Task<ContactPersonType?> GetContactPersonTypeAsync(Guid id, CancellationToken ct = default);
    Task<(List<Company> Items, int TotalCount)> SearchCompaniesAsync(
        Guid tenantId, Guid orgId, string? search, Guid? companyTypeId, bool? isActive,
        int page, int pageSize, CancellationToken ct = default);
    Task<Company?> GetCompanyAsync(Guid tenantId, Guid orgId, Guid id, CancellationToken ct = default);
    Task<bool> CompanyNameExistsAsync(
        Guid tenantId, Guid orgId, Guid companyTypeId, string normalizedName,
        Guid? excludingId = null, CancellationToken ct = default);
    Task AddCompanyAsync(Company company, CancellationToken ct = default);
    Task UpdateCompanyAsync(Company company, CancellationToken ct = default);
    Task<List<CompanyContactPerson>> GetContactPersonsAsync(
        Guid tenantId, Guid companyId, bool? isActive, CancellationToken ct = default);
    Task<CompanyContactPerson?> GetContactPersonAsync(
        Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default);
    Task AddContactPersonAsync(CompanyContactPerson contact, CancellationToken ct = default);
    Task UpdateContactPersonAsync(CompanyContactPerson contact, CancellationToken ct = default);
}
