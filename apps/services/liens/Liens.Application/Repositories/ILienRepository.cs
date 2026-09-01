using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienRepository
{
    Task<Lien?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<Lien?> GetByIdAnyTenantAsync(Guid id, CancellationToken ct = default);
    Task<Lien?> GetByLienNumberAsync(Guid tenantId, string lienNumber, CancellationToken ct = default);
    Task<Lien?> GetByExternalReferenceAsync(Guid tenantId, string externalReference, CancellationToken ct = default);
    Task<(List<Lien> Items, int TotalCount)> SearchAsync(
        Guid tenantId,
        string? search,
        string? status,
        string? lienType,
        Guid? caseId,
        Guid? facilityId,
        int page,
        int pageSize,
        CancellationToken ct = default,
        DateTime? createdFromUtc = null,
        DateTime? createdToUtc = null,
        Guid? visibleOrgId = null,
        bool includeSellerOrg = false,
        bool includeBuyerOrg = false,
        bool includeHolderOrg = false,
        bool includeMarketplace = false,
        bool excludeRejectedAndCancelled = false);
    Task<List<Lien>> SearchReportAsync(
        Guid tenantId,
        string? search,
        IReadOnlyCollection<string> lienStatuses,
        IReadOnlyCollection<string> caseStatuses,
        DateOnly? purchaseDateFrom,
        DateOnly? purchaseDateTo,
        DateTime? closedDateFrom,
        DateTime? closedDateTo,
        bool useSettlementDateForClosedFilter,
        string? isBulk,
        IReadOnlyCollection<Guid> caseIds,
        CancellationToken ct = default);
    Task<List<Lien>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<List<Lien>> GetByFacilityIdAsync(Guid tenantId, Guid facilityId, CancellationToken ct = default);
    Task AddAsync(Lien entity, CancellationToken ct = default);
    Task UpdateAsync(Lien entity, CancellationToken ct = default);
}
