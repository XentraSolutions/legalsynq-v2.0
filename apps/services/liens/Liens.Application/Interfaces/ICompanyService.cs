using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ICompanyService
{
    Task<List<CompanyTypeResponse>> GetCompanyTypesAsync(CancellationToken ct = default);
    Task<List<ContactPersonTypeResponse>> GetContactPersonTypesAsync(Guid companyTypeId, CancellationToken ct = default);
    Task<PaginatedResult<CompanyResponse>> SearchCompaniesAsync(
        Guid tenantId, Guid orgId, string? search, Guid? companyTypeId, bool? isActive,
        int page, int pageSize, CancellationToken ct = default);
    Task<CompanyResponse?> GetCompanyAsync(Guid tenantId, Guid orgId, Guid id, CancellationToken ct = default);
    Task<CompanyResponse> CreateCompanyAsync(
        Guid tenantId, Guid orgId, Guid actingUserId, CreateCompanyRequest request, CancellationToken ct = default);
    Task<CompanyResponse> UpdateCompanyAsync(
        Guid tenantId, Guid orgId, Guid id, Guid actingUserId, UpdateCompanyRequest request, CancellationToken ct = default);
    Task<CompanyResponse> SetCompanyActiveAsync(
        Guid tenantId, Guid orgId, Guid id, Guid actingUserId, bool isActive, CancellationToken ct = default);
    Task<List<CompanyContactPersonResponse>> GetContactPersonsAsync(
        Guid tenantId, Guid orgId, Guid companyId, bool? isActive, CancellationToken ct = default);
    Task<CompanyContactPersonResponse?> GetContactPersonAsync(
        Guid tenantId, Guid orgId, Guid companyId, Guid contactId, CancellationToken ct = default);
    Task<CompanyContactPersonResponse> CreateContactPersonAsync(
        Guid tenantId, Guid orgId, Guid companyId, Guid actingUserId,
        CreateCompanyContactPersonRequest request, CancellationToken ct = default);
    Task<CompanyContactPersonResponse> UpdateContactPersonAsync(
        Guid tenantId, Guid orgId, Guid companyId, Guid contactId, Guid actingUserId,
        UpdateCompanyContactPersonRequest request, CancellationToken ct = default);
    Task<CompanyContactPersonResponse> SetContactPersonActiveAsync(
        Guid tenantId, Guid orgId, Guid companyId, Guid contactId, Guid actingUserId,
        bool isActive, CancellationToken ct = default);
}
