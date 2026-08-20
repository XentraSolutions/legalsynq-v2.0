using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ICaseService
{
    Task<PaginatedResult<CaseResponse>> SearchAsync(
        Guid tenantId, string? search, string? status, int page, int pageSize,
        Guid? orgId = null,
        CancellationToken ct = default);

    Task<PaginatedResult<CaseResponse>> SearchV3Async(
        Guid tenantId,
        string? keyword,
        string? statusId,
        int page,
        int limit,
        string? sortBy,
        string? sortDirection,
        Guid? lawFirmOrgId = null,
        string? accidentTypeId = null,
        string? caseManagerId = null,
        string? lawFirmIds = null,
        CancellationToken ct = default);

    Task<CaseResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<CaseResponse?> GetByCaseNumberAsync(Guid tenantId, string caseNumber, CancellationToken ct = default);

    Task<CaseDuplicateCheckResponse> CheckDuplicatesAsync(
        Guid tenantId, CaseDuplicateCheckRequest request, CancellationToken ct = default);

    Task<CaseResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateCaseRequest request, CancellationToken ct = default);

    Task<CaseResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateCaseRequest request, CancellationToken ct = default);

    Task<bool> ReassignLawFirmAsync(
        Guid tenantId,
        Guid caseId,
        Guid lawFirmOrgId,
        Guid actingUserId,
        CancellationToken ct = default);

    Task<bool> ReassignCaseManagerAsync(
        Guid tenantId,
        Guid caseId,
        Guid caseManagerId,
        Guid actingUserId,
        CancellationToken ct = default);
}
