using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ICompanyService
{
    Task<List<CompanyTypeResponse>> GetCompanyTypesAsync(CancellationToken ct = default);
    Task<List<ContactPersonTypeResponse>> GetContactPersonTypesAsync(
        Guid tenantId, Guid orgId, Guid companyTypeId, CancellationToken ct = default);
    Task<ContactPersonTypeResponse> CreateContactPersonTypeAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateContactPersonTypeRequest request, CancellationToken ct = default);
    Task<PaginatedResult<CompanyResponse>> SearchCompaniesAsync(
        Guid tenantId, Guid orgId, string? search, Guid? companyTypeId, bool? isActive,
        int page, int pageSize, CancellationToken ct = default);
    Task<List<CompanyResponse>> GetCompaniesForExportAsync(
        Guid tenantId, Guid orgId, string? search, Guid? companyTypeId, bool? isActive,
        CancellationToken ct = default);
    Task<CompanyResponse?> GetCompanyAsync(Guid tenantId, Guid orgId, Guid id, CancellationToken ct = default);
    Task<CompanyDetailsResponse?> GetCompanyDetailsAsync(
        Guid tenantId, Guid orgId, Guid companyId, int page, int pageSize,
        CancellationToken ct = default);
    Task<CompanyResponse> CreateCompanyAsync(
        Guid tenantId, Guid orgId, Guid actingUserId, CreateCompanyRequest request, CancellationToken ct = default);
    Task<CompanyResponse> UpdateCompanyAsync(
        Guid tenantId, Guid orgId, Guid id, Guid actingUserId, UpdateCompanyRequest request, CancellationToken ct = default);
    Task<CompanyResponse> SetCompanyActiveAsync(
        Guid tenantId, Guid orgId, Guid id, Guid actingUserId, bool isActive, CancellationToken ct = default);
    Task<CompanyReassignmentResponse> ReassignCompanyAsync(
        Guid tenantId, Guid orgId, Guid sourceCompanyId, Guid targetCompanyId,
        Guid actingUserId, CancellationToken ct = default);
    Task<List<CompanyContactPersonResponse>> GetContactPersonsAsync(
        Guid tenantId, Guid orgId, Guid companyId, bool? isActive, CancellationToken ct = default);
    Task<ContactPersonDirectoryResponse> SearchContactPersonsAsync(
        Guid tenantId, Guid orgId, string? search, Guid? companyTypeId,
        Guid? contactPersonTypeId, bool? isActive, int page, int pageSize,
        CancellationToken ct = default);
    Task<List<CompanyContactPersonExportResponse>> GetContactPersonsForExportAsync(
        Guid tenantId, Guid orgId, Guid? companyId, string? search, Guid? companyTypeId,
        Guid? contactPersonTypeId, bool? isActive, CancellationToken ct = default);
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
    Task<CompanyContactPersonReassignmentResponse> ReassignContactPersonAsync(
        Guid tenantId, Guid orgId, Guid sourceCompanyId, Guid sourceContactPersonId,
        Guid targetContactPersonId, Guid actingUserId, CancellationToken ct = default);
}
