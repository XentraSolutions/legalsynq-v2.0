using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;

namespace Liens.Application.Services;

public class SettlementService : ISettlementService
{
    private readonly ILienReductionRepository          _reductionRepo;
    private readonly ILienSettlementRepository         _settlementRepo;
    private readonly ISettlementPaymentDetailRepository _paymentRepo;

    public SettlementService(
        ILienReductionRepository reductionRepo,
        ILienSettlementRepository settlementRepo,
        ISettlementPaymentDetailRepository paymentRepo)
    {
        _reductionRepo  = reductionRepo;
        _settlementRepo = settlementRepo;
        _paymentRepo    = paymentRepo;
    }

    // ── Reductions ────────────────────────────────────────────────────────────

    public async Task<List<LienReductionResponse>> GetReductionsByCaseAsync(
        Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        var items = await _reductionRepo.GetByCaseIdAsync(tenantId, caseId, ct);
        return items.Select(MapReduction).ToList();
    }

    public async Task<List<LienReductionResponse>> GetReductionsByLienAsync(
        Guid tenantId, Guid lienId, CancellationToken ct = default)
    {
        var items = await _reductionRepo.GetByLienIdAsync(tenantId, lienId, ct);
        return items.Select(MapReduction).ToList();
    }

    public async Task<LienReductionResponse> CreateReductionAsync(
        Guid tenantId, Guid userId, CreateLienReductionRequest request, CancellationToken ct = default)
    {
        var entity = LienReduction.Create(
            tenantId, request.CaseId, request.LienId,
            request.ReductionDate, request.Amount, userId, request.Note);
        await _reductionRepo.AddAsync(entity, ct);
        return MapReduction(entity);
    }

    public async Task<LienReductionResponse> UpdateReductionAsync(
        Guid tenantId, Guid id, Guid userId, UpdateLienReductionRequest request, CancellationToken ct = default)
    {
        var entity = await _reductionRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Reduction {id} not found.");
        entity.Update(request.ReductionDate, request.Amount, userId, request.Note);
        await _reductionRepo.UpdateAsync(entity, ct);
        return MapReduction(entity);
    }

    private static LienReductionResponse MapReduction(LienReduction r) => new()
    {
        Id            = r.Id,
        TenantId      = r.TenantId,
        CaseId        = r.CaseId,
        LienId        = r.LienId,
        ReductionDate = r.ReductionDate,
        Amount        = r.Amount,
        Note          = r.Note,
        CreatedAtUtc  = r.CreatedAtUtc,
        UpdatedAtUtc  = r.UpdatedAtUtc,
    };

    // ── Settlements ───────────────────────────────────────────────────────────

    public async Task<List<LienSettlementResponse>> GetSettlementsByCaseAsync(
        Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        var items = await _settlementRepo.GetByCaseIdAsync(tenantId, caseId, ct);
        return items.Select(MapSettlement).ToList();
    }

    public async Task<List<LienSettlementResponse>> GetSettlementsByLienAsync(
        Guid tenantId, Guid lienId, CancellationToken ct = default)
    {
        var items = await _settlementRepo.GetByLienIdAsync(tenantId, lienId, ct);
        return items.Select(MapSettlement).ToList();
    }

    public async Task<LienSettlementResponse> CreateSettlementAsync(
        Guid tenantId, Guid userId, CreateLienSettlementRequest request, CancellationToken ct = default)
    {
        var entity = LienSettlement.Create(
            tenantId, request.CaseId, request.LienId,
            request.PaymentNumber, request.Amount, userId,
            request.Status, request.Note);
        await _settlementRepo.AddAsync(entity, ct);
        return MapSettlement(entity);
    }

    public async Task<LienSettlementResponse> UpdateSettlementAsync(
        Guid tenantId, Guid id, Guid userId, UpdateLienSettlementRequest request, CancellationToken ct = default)
    {
        var entity = await _settlementRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Settlement {id} not found.");
        entity.Update(request.PaymentNumber, request.Amount, userId, request.Status, request.Note);
        await _settlementRepo.UpdateAsync(entity, ct);
        return MapSettlement(entity);
    }

    private static LienSettlementResponse MapSettlement(LienSettlement s) => new()
    {
        Id            = s.Id,
        TenantId      = s.TenantId,
        CaseId        = s.CaseId,
        LienId        = s.LienId,
        PaymentNumber = s.PaymentNumber,
        Amount        = s.Amount,
        Status        = s.Status,
        Note          = s.Note,
        CreatedAtUtc  = s.CreatedAtUtc,
        UpdatedAtUtc  = s.UpdatedAtUtc,
    };

    // ── Payment Details ───────────────────────────────────────────────────────

    public async Task<List<SettlementPaymentDetailResponse>> GetPaymentsByCaseAsync(
        Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        var items = await _paymentRepo.GetByCaseIdAsync(tenantId, caseId, ct);
        return items.Select(MapPayment).ToList();
    }

    public async Task<List<SettlementPaymentDetailResponse>> GetPaymentsByLienAsync(
        Guid tenantId, Guid lienId, CancellationToken ct = default)
    {
        var items = await _paymentRepo.GetByLienIdAsync(tenantId, lienId, ct);
        return items.Select(MapPayment).ToList();
    }

    public async Task<SettlementPaymentDetailResponse> CreatePaymentAsync(
        Guid tenantId, Guid userId, CreateSettlementPaymentDetailRequest request, CancellationToken ct = default)
    {
        var entity = SettlementPaymentDetail.Create(
            tenantId, request.CaseId, request.LienId,
            request.PaymentNumber, request.Amount, userId,
            request.PaymentDate, request.Payee, request.CheckNumber, request.Note);
        await _paymentRepo.AddAsync(entity, ct);
        return MapPayment(entity);
    }

    public async Task DeletePaymentAsync(
        Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _paymentRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Payment {id} not found.");
        entity.SoftDelete(userId);
        await _paymentRepo.SoftDeleteAsync(entity, ct);
    }

    private static SettlementPaymentDetailResponse MapPayment(SettlementPaymentDetail p) => new()
    {
        Id            = p.Id,
        TenantId      = p.TenantId,
        CaseId        = p.CaseId,
        LienId        = p.LienId,
        PaymentNumber = p.PaymentNumber,
        Amount        = p.Amount,
        PaymentDate   = p.PaymentDate,
        Payee         = p.Payee,
        CheckNumber   = p.CheckNumber,
        Note          = p.Note,
        CreatedAtUtc  = p.CreatedAtUtc,
        UpdatedAtUtc  = p.UpdatedAtUtc,
    };
}
