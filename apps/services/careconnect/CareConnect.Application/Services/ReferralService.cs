using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using System.Text.RegularExpressions;

namespace CareConnect.Application.Services;

public class ReferralService : IReferralService
{
    private readonly IReferralRepository _referrals;
    private readonly IProviderRepository _providers;

    public ReferralService(IReferralRepository referrals, IProviderRepository providers)
    {
        _referrals = referrals;
        _providers = providers;
    }

    public async Task<List<ReferralResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var referrals = await _referrals.GetAllByTenantAsync(tenantId, ct);
        return referrals.Select(ToResponse).ToList();
    }

    public async Task<ReferralResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var referral = await _referrals.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Referral '{id}' was not found.");
        return ToResponse(referral);
    }

    public async Task<ReferralResponse> CreateAsync(Guid tenantId, Guid? userId, CreateReferralRequest request, CancellationToken ct = default)
    {
        ValidateCreate(request);

        var provider = await _providers.GetByIdAsync(tenantId, request.ProviderId, ct)
            ?? throw new NotFoundException($"Provider '{request.ProviderId}' was not found.");

        var referral = Referral.Create(
            tenantId,
            provider.Id,
            request.ClientFirstName,
            request.ClientLastName,
            request.ClientDob,
            request.ClientPhone,
            request.ClientEmail,
            request.CaseNumber,
            request.RequestedService,
            request.Urgency,
            request.Notes,
            userId);

        await _referrals.AddAsync(referral, ct);

        var loaded = await _referrals.GetByIdAsync(tenantId, referral.Id, ct);
        return ToResponse(loaded!);
    }

    public async Task<ReferralResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateReferralRequest request, CancellationToken ct = default)
    {
        var referral = await _referrals.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Referral '{id}' was not found.");

        ValidateUpdate(request);

        ReferralStatusHistory? history = null;

        if (referral.Status != request.Status)
        {
            ReferralWorkflowRules.ValidateTransition(referral.Status, request.Status);

            history = ReferralStatusHistory.Create(
                referral.Id,
                tenantId,
                referral.Status,
                request.Status,
                userId,
                request.Notes);
        }

        referral.Update(request.RequestedService, request.Urgency, request.Status, request.Notes, userId);
        await _referrals.UpdateAsync(referral, history, ct);

        var loaded = await _referrals.GetByIdAsync(tenantId, referral.Id, ct);
        return ToResponse(loaded!);
    }

    public async Task<List<ReferralStatusHistoryResponse>> GetHistoryAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
    {
        _ = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        var history = await _referrals.GetHistoryByReferralAsync(tenantId, referralId, ct);
        return history.Select(ToHistoryResponse).ToList();
    }

    private static void ValidateCreate(CreateReferralRequest r)
    {
        var errors = new Dictionary<string, string[]>();

        if (r.ProviderId == Guid.Empty)
            errors["providerId"] = new[] { "ProviderId is required." };

        if (string.IsNullOrWhiteSpace(r.ClientFirstName))
            errors["clientFirstName"] = new[] { "ClientFirstName is required." };

        if (string.IsNullOrWhiteSpace(r.ClientLastName))
            errors["clientLastName"] = new[] { "ClientLastName is required." };

        if (string.IsNullOrWhiteSpace(r.ClientPhone))
            errors["clientPhone"] = new[] { "ClientPhone is required." };

        if (string.IsNullOrWhiteSpace(r.ClientEmail))
            errors["clientEmail"] = new[] { "ClientEmail is required." };
        else if (!Regex.IsMatch(r.ClientEmail.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            errors["clientEmail"] = new[] { "ClientEmail format is invalid." };

        if (string.IsNullOrWhiteSpace(r.RequestedService))
            errors["requestedService"] = new[] { "RequestedService is required." };

        if (!Referral.ValidUrgencies.All.Contains(r.Urgency))
            errors["urgency"] = new[] { $"Urgency must be one of: {string.Join(", ", Referral.ValidUrgencies.All)}." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static void ValidateUpdate(UpdateReferralRequest r)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(r.RequestedService))
            errors["requestedService"] = new[] { "RequestedService is required." };

        if (!Referral.ValidUrgencies.All.Contains(r.Urgency))
            errors["urgency"] = new[] { $"Urgency must be one of: {string.Join(", ", Referral.ValidUrgencies.All)}." };

        if (!Referral.ValidStatuses.All.Contains(r.Status))
            errors["status"] = new[] { $"Status must be one of: {string.Join(", ", Referral.ValidStatuses.All)}." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static ReferralResponse ToResponse(Referral r) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        ProviderId = r.ProviderId,
        ProviderName = r.Provider?.Name ?? string.Empty,
        ClientFirstName = r.ClientFirstName,
        ClientLastName = r.ClientLastName,
        ClientDob = r.ClientDob,
        ClientPhone = r.ClientPhone,
        ClientEmail = r.ClientEmail,
        CaseNumber = r.CaseNumber,
        RequestedService = r.RequestedService,
        Urgency = r.Urgency,
        Status = r.Status,
        Notes = r.Notes,
        CreatedAtUtc = r.CreatedAtUtc,
        UpdatedAtUtc = r.UpdatedAtUtc
    };

    private static ReferralStatusHistoryResponse ToHistoryResponse(ReferralStatusHistory h) => new()
    {
        Id = h.Id,
        ReferralId = h.ReferralId,
        OldStatus = h.OldStatus,
        NewStatus = h.NewStatus,
        ChangedByUserId = h.ChangedByUserId,
        ChangedAtUtc = h.ChangedAtUtc,
        Notes = h.Notes
    };
}
