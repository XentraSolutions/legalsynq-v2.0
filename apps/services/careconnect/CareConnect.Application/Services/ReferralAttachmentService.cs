using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class ReferralAttachmentService : IReferralAttachmentService
{
    private readonly IReferralAttachmentRepository _attachments;
    private readonly IReferralRepository _referrals;

    public ReferralAttachmentService(IReferralAttachmentRepository attachments, IReferralRepository referrals)
    {
        _attachments = attachments;
        _referrals   = referrals;
    }

    public async Task<List<AttachmentMetadataResponse>> GetByReferralAsync(
        Guid tenantId,
        Guid referralId,
        CancellationToken ct = default)
    {
        _ = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        var rows = await _attachments.GetByReferralAsync(tenantId, referralId, ct);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<AttachmentMetadataResponse> CreateAsync(
        Guid tenantId,
        Guid referralId,
        Guid? userId,
        CreateAttachmentMetadataRequest request,
        CancellationToken ct = default)
    {
        _ = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        ValidateAttachmentRequest(request);

        var attachment = ReferralAttachment.Create(
            tenantId,
            referralId,
            request.FileName,
            request.ContentType,
            request.FileSizeBytes,
            request.ExternalDocumentId,
            request.ExternalStorageProvider,
            request.Status,
            request.Notes,
            userId);

        await _attachments.AddAsync(attachment, ct);
        return ToResponse(attachment);
    }

    private static void ValidateAttachmentRequest(CreateAttachmentMetadataRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.FileName))
            errors["fileName"] = new[] { "FileName is required." };
        else if (request.FileName.Length > 500)
            errors["fileName"] = new[] { "FileName must not exceed 500 characters." };

        if (string.IsNullOrWhiteSpace(request.ContentType))
            errors["contentType"] = new[] { "ContentType is required." };
        else if (request.ContentType.Length > 200)
            errors["contentType"] = new[] { "ContentType must not exceed 200 characters." };

        if (request.FileSizeBytes < 0)
            errors["fileSizeBytes"] = new[] { "FileSizeBytes must be 0 or greater." };

        if (string.IsNullOrWhiteSpace(request.Status) || !AttachmentStatus.IsValid(request.Status))
            errors["status"] = new[] { $"'{request.Status}' is not a valid status. Allowed: {string.Join(", ", AttachmentStatus.All)}." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static AttachmentMetadataResponse ToResponse(ReferralAttachment a) => new()
    {
        Id                      = a.Id,
        FileName                = a.FileName,
        ContentType             = a.ContentType,
        FileSizeBytes           = a.FileSizeBytes,
        ExternalDocumentId      = a.ExternalDocumentId,
        ExternalStorageProvider = a.ExternalStorageProvider,
        Status                  = a.Status,
        Notes                   = a.Notes,
        CreatedAtUtc            = a.CreatedAtUtc,
        CreatedByUserId         = a.CreatedByUserId
    };
}
