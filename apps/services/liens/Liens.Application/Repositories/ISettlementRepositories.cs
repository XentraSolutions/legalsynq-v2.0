using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienReductionRepository
{
    Task<LienReduction?>      GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<LienReduction>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<List<LienReduction>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);
    Task AddAsync(LienReduction reduction, CancellationToken ct = default);
    Task UpdateAsync(LienReduction reduction, CancellationToken ct = default);
}

public interface ILienSettlementRepository
{
    Task<LienSettlement?>      GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<LienSettlement>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<List<LienSettlement>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);
    Task<List<LienSettlement>> GetByLienIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> lienIds, CancellationToken ct = default);
    Task AddAsync(LienSettlement settlement, CancellationToken ct = default);
    Task UpdateAsync(LienSettlement settlement, CancellationToken ct = default);
}

public interface ISettlementPaymentDetailRepository
{
    Task<SettlementPaymentDetail?>      GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<SettlementPaymentDetail>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<List<SettlementPaymentDetail>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);
    Task<List<SettlementPaymentDetail>> GetByLienIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> lienIds, CancellationToken ct = default);
    Task AddAsync(SettlementPaymentDetail detail, CancellationToken ct = default);
    Task SoftDeleteAsync(SettlementPaymentDetail detail, CancellationToken ct = default);
}
