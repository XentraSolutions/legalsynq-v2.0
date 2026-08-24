using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using System.Globalization;

namespace Liens.Application.Services;

public class SettlementService : ISettlementService
{
    private const string LegacyMetadataMarker = "[legacy-meta]";

    private readonly ILienReductionRepository          _reductionRepo;
    private readonly ILienSettlementRepository         _settlementRepo;
    private readonly ISettlementPaymentDetailRepository _paymentRepo;
    private readonly ILienService                       _lienService;

    public SettlementService(
        ILienReductionRepository reductionRepo,
        ILienSettlementRepository settlementRepo,
        ISettlementPaymentDetailRepository paymentRepo,
        ILienService lienService)
    {
        _reductionRepo  = reductionRepo;
        _settlementRepo = settlementRepo;
        _paymentRepo    = paymentRepo;
        _lienService    = lienService;
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
        CreatedByUserId = r.CreatedByUserId,
        UpdatedByUserId = r.UpdatedByUserId,
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
        if (IsLienStatusSyncRequest(request.Status))
        {
            // Legacy medical status normalization maps Open to Active and Closed
            // to Settled while retaining the status transition audit/history.
            await _lienService.SetLegacyMedicalStatusAsync(
                tenantId, request.LienId, userId, request.Status!, ct);
        }

        var entity = LienSettlement.Create(
            tenantId, request.CaseId, request.LienId,
            request.PaymentNumber, request.Amount, userId,
            request.Status, request.Note, request.SettlementDate);
        await _settlementRepo.AddAsync(entity, ct);
        return MapSettlement(entity);
    }

    private static bool IsLienStatusSyncRequest(string? status) =>
        string.Equals(status?.Trim(), "Open", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status?.Trim(), "Closed", StringComparison.OrdinalIgnoreCase);

    public async Task<LienSettlementResponse> UpdateSettlementAsync(
        Guid tenantId, Guid id, Guid userId, UpdateLienSettlementRequest request, CancellationToken ct = default)
    {
        var entity = await _settlementRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Settlement {id} not found.");
        entity.Update(
            request.PaymentNumber,
            request.Amount,
            userId,
            request.Status,
            request.Note,
            request.SettlementDate ?? entity.SettlementDate);
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
        SettlementDate = s.SettlementDate,
        Status        = s.Status,
        Note          = s.Note,
        CreatedAtUtc  = s.CreatedAtUtc,
        UpdatedAtUtc  = s.UpdatedAtUtc,
        CreatedByUserId = s.CreatedByUserId,
        UpdatedByUserId = s.UpdatedByUserId,
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
        var usesLegacyFieldPositions = IsLienStatusSyncRequest(request.SettlementStatus) &&
                                       string.IsNullOrWhiteSpace(request.LienStatus) &&
                                       !IsSettlementTypeCode(request.SettlementType);
        var settlementType = FirstNonEmpty(
            usesLegacyFieldPositions ? null : request.SettlementType,
            request.Type) ?? "other";
        var settlementStatus = FirstNonEmpty(
            usesLegacyFieldPositions ? request.SettlementType : request.SettlementStatus,
            request.Status);
        var requestedLienStatus = FirstNonEmpty(
            request.LienStatus,
            IsLienStatusSyncRequest(request.SettlementStatus) ? request.SettlementStatus : null);
        if (IsLienStatusSyncRequest(requestedLienStatus))
        {
            await _lienService.SetLegacyMedicalStatusAsync(
                tenantId, request.LienId, userId, requestedLienStatus!, ct);
        }

        var paymentNumber = request.PaymentNumber > 0
            ? request.PaymentNumber
            : await GetNextPaymentNumberAsync(tenantId, request.CaseId, ct);
        var entity = SettlementPaymentDetail.Create(
            tenantId, request.CaseId, request.LienId,
            paymentNumber, request.Amount, userId,
            request.PaymentDate,
            request.Payee,
            FirstNonEmpty(request.CheckNumber, request.ReferenceNumber),
            BuildPaymentNote(request, settlementType, settlementStatus));
        await _paymentRepo.AddAsync(entity, ct);
        return MapPayment(entity);
    }

    public async Task<SettlementPaymentDetailResponse> UpdatePaymentAsync(
        Guid tenantId,
        Guid id,
        Guid userId,
        UpdateSettlementPaymentDetailRequest request,
        CancellationToken ct = default)
    {
        var entity = await _paymentRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Settlement payment '{id}' not found.");

        var errors = new Dictionary<string, string[]>();
        if (request.Amount < 0)
            errors["amount"] = ["amount cannot be negative."];
        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            errors["paymentMethod"] = ["paymentMethod is required."];
        else if (ContainsReservedPaymentMetadataSyntax(request.PaymentMethod))
            errors["paymentMethod"] = ["paymentMethod contains reserved payment metadata syntax."];
        if (string.IsNullOrWhiteSpace(request.ReferenceNumber))
            errors["referenceNumber"] = ["referenceNumber is required."];
        else if (request.ReferenceNumber.Trim().Length > 100)
            errors["referenceNumber"] = ["referenceNumber cannot exceed 100 characters."];
        if (request.Notes is null)
            errors["notes"] = ["notes is required but may be empty."];
        else if (request.Notes.Contains(LegacyMetadataMarker, StringComparison.Ordinal))
            errors["notes"] = ["notes cannot contain the reserved [legacy-meta] marker."];
        if (string.IsNullOrWhiteSpace(request.SettlementType))
            errors["settlementType"] = ["settlementType is required."];
        else if (ContainsReservedPaymentMetadataSyntax(request.SettlementType))
            errors["settlementType"] = ["settlementType contains reserved payment metadata syntax."];
        if (string.IsNullOrWhiteSpace(request.SettlementStatus))
            errors["settlementStatus"] = ["settlementStatus is required."];
        else if (ContainsReservedPaymentMetadataSyntax(request.SettlementStatus))
            errors["settlementStatus"] = ["settlementStatus contains reserved payment metadata syntax."];
        if (string.IsNullOrWhiteSpace(request.LienStatus))
            errors["lienStatus"] = ["lienStatus is required."];
        else if (!IsSupportedLienStatus(request.LienStatus))
            errors["lienStatus"] = ["lienStatus must be Open, Closed, or a canonical lien status."];

        var metadata = ParsePaymentMetadata(entity.Note);
        UpdateMetadata(metadata, "paymentMethod", request.PaymentMethod);
        UpdateMetadata(metadata, "type", request.SettlementType);
        UpdateMetadata(metadata, "status", request.SettlementStatus);
        var updatedNote = SerializePaymentNote(request.Notes, metadata);
        if (updatedNote.Length > 1000)
            errors["notes"] = ["notes and payment metadata cannot exceed 1000 characters."];

        if (errors.Count > 0)
            throw new ValidationException("One or more settlement payment fields are invalid.", errors);

        await _lienService.SetLegacyMedicalStatusAsync(
            tenantId, entity.LienId, userId, request.LienStatus!, ct);

        entity.Update(
            request.Amount,
            request.PaymentDate,
            request.ReferenceNumber,
            updatedNote,
            userId);
        await _paymentRepo.UpdateAsync(entity, ct);

        return MapPayment(entity);
    }

    private async Task<int> GetNextPaymentNumberAsync(
        Guid tenantId,
        Guid caseId,
        CancellationToken ct)
    {
        var existing = await _paymentRepo.GetByCaseIdAsync(tenantId, caseId, ct);
        var usedNumbers = existing
            .Where(payment => payment.PaymentNumber > 0)
            .Select(payment => payment.PaymentNumber)
            .ToHashSet();
        var nextFallback = 1;

        foreach (var _ in existing
                     .Where(payment => payment.PaymentNumber <= 0)
                     .OrderBy(payment => payment.CreatedAtUtc)
                     .ThenBy(payment => payment.Id))
        {
            while (usedNumbers.Contains(nextFallback))
                nextFallback++;

            usedNumbers.Add(nextFallback++);
        }

        if (usedNumbers.Count == 0)
            return 1;

        var next = (long)usedNumbers.Max() + 1;
        if (next > int.MaxValue)
            throw new InvalidOperationException($"No payment numbers remain available for case '{caseId}'.");

        return (int)next;
    }

    public async Task DeletePaymentAsync(
        Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _paymentRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Payment {id} not found.");
        entity.SoftDelete(userId);
        await _paymentRepo.SoftDeleteAsync(entity, ct);
    }

    private static SettlementPaymentDetailResponse MapPayment(SettlementPaymentDetail p)
    {
        var metadata = ParsePaymentMetadata(p.Note);
        return new SettlementPaymentDetailResponse
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
            Note          = ExtractPaymentNote(p.Note),
            PaymentMethod = metadata.GetValueOrDefault("paymentMethod"),
            SettlementTypeId = FirstNonEmpty(metadata.GetValueOrDefault("type")) ?? "other",
            SettlementStatusId = metadata.GetValueOrDefault("status"),
            NetProfit     = ParseLegacyDecimal(metadata.GetValueOrDefault("netProfit")),
            CreatedAtUtc  = p.CreatedAtUtc,
            UpdatedAtUtc  = p.UpdatedAtUtc,
            CreatedByUserId = p.CreatedByUserId,
            UpdatedByUserId = p.UpdatedByUserId,
        };
    }

    private static string? BuildPaymentNote(
        CreateSettlementPaymentDetailRequest request,
        string? settlementType,
        string? settlementStatus)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["netProfit"] = (request.NetProfit ?? 0m).ToString("0.00", CultureInfo.InvariantCulture),
        };
        SetMetadata(metadata, "paymentMethod", request.PaymentMethod);
        SetMetadata(metadata, "type", settlementType);
        SetMetadata(metadata, "status", settlementStatus);

        return SerializePaymentNote(FirstNonEmpty(request.Note, request.Notes), metadata);
    }

    private static Dictionary<string, string> ParsePaymentMetadata(string? note)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(note))
            return metadata;

        var rawMetadata = note;
        var markerIndex = note.IndexOf(LegacyMetadataMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
            rawMetadata = note[(markerIndex + LegacyMetadataMarker.Length)..];

        foreach (var segment in rawMetadata.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = segment[..separator].Trim();
            var value = segment[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                metadata[key] = value;
        }

        return metadata;
    }

    private static string? ExtractPaymentNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return null;

        var markerIndex = note.IndexOf(LegacyMetadataMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
            return FirstNonEmpty(note[..markerIndex]);

        return note.Contains("legacyPaymentDetailId=", StringComparison.OrdinalIgnoreCase)
            ? null
            : note.Trim();
    }

    private static decimal? ParseLegacyDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static bool IsSettlementTypeCode(string? value) =>
        string.Equals(value, "by_attorney", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "by_medical_provider", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "by_funding_company", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "other", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedLienStatus(string value) =>
        string.Equals(value.Trim(), "Open", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value.Trim(), "Closed", StringComparison.OrdinalIgnoreCase) ||
        LienStatus.All.Contains(value.Trim());

    private static bool ContainsReservedPaymentMetadataSyntax(string value) =>
        value.Contains(';') ||
        value.Contains('=') ||
        value.Contains('\r') ||
        value.Contains('\n') ||
        value.Contains(LegacyMetadataMarker, StringComparison.Ordinal);

    private static string SerializePaymentNote(
        string? note,
        IReadOnlyDictionary<string, string> metadata)
    {
        var serializedMetadata = string.Join("; ", metadata.Select(pair => $"{pair.Key}={pair.Value}"));
        return string.IsNullOrWhiteSpace(note)
            ? $"{LegacyMetadataMarker}{Environment.NewLine}{serializedMetadata}"
            : $"{note.Trim()}{Environment.NewLine}{LegacyMetadataMarker}{Environment.NewLine}{serializedMetadata}";
    }

    private static void UpdateMetadata(
        IDictionary<string, string> metadata,
        string key,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            metadata.Remove(key);
        else
            metadata[key] = value.Trim();
    }

    private static void SetMetadata(
        IDictionary<string, string> metadata,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            metadata[key] = value.Trim();
    }
}
