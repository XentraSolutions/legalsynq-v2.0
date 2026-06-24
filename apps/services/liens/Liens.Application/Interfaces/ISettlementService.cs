using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ISettlementService
{
    // Reductions
    Task<List<LienReductionResponse>> GetReductionsByCaseAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<List<LienReductionResponse>> GetReductionsByLienAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);
    Task<LienReductionResponse>       CreateReductionAsync(Guid tenantId, Guid userId, CreateLienReductionRequest request, CancellationToken ct = default);
    Task<LienReductionResponse>       UpdateReductionAsync(Guid tenantId, Guid id, Guid userId, UpdateLienReductionRequest request, CancellationToken ct = default);

    // Settlements
    Task<List<LienSettlementResponse>> GetSettlementsByCaseAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<List<LienSettlementResponse>> GetSettlementsByLienAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);
    Task<LienSettlementResponse>       CreateSettlementAsync(Guid tenantId, Guid userId, CreateLienSettlementRequest request, CancellationToken ct = default);
    Task<LienSettlementResponse>       UpdateSettlementAsync(Guid tenantId, Guid id, Guid userId, UpdateLienSettlementRequest request, CancellationToken ct = default);

    // Payment details
    Task<List<SettlementPaymentDetailResponse>> GetPaymentsByCaseAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<List<SettlementPaymentDetailResponse>> GetPaymentsByLienAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);
    Task<SettlementPaymentDetailResponse>       CreatePaymentAsync(Guid tenantId, Guid userId, CreateSettlementPaymentDetailRequest request, CancellationToken ct = default);
    Task DeletePaymentAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default);
}
