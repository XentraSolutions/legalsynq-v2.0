using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class AppointmentAttachmentService : IAppointmentAttachmentService
{
    private readonly IAppointmentAttachmentRepository _attachments;
    private readonly IAppointmentRepository _appointments;

    public AppointmentAttachmentService(IAppointmentAttachmentRepository attachments, IAppointmentRepository appointments)
    {
        _attachments  = attachments;
        _appointments = appointments;
    }

    public async Task<List<AttachmentMetadataResponse>> GetByAppointmentAsync(
        Guid tenantId,
        Guid appointmentId,
        CancellationToken ct = default)
    {
        _ = await _appointments.GetByIdAsync(tenantId, appointmentId, ct)
            ?? throw new NotFoundException($"Appointment '{appointmentId}' was not found.");

        var rows = await _attachments.GetByAppointmentAsync(tenantId, appointmentId, ct);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<AttachmentMetadataResponse> CreateAsync(
        Guid tenantId,
        Guid appointmentId,
        Guid? userId,
        CreateAttachmentMetadataRequest request,
        CancellationToken ct = default)
    {
        _ = await _appointments.GetByIdAsync(tenantId, appointmentId, ct)
            ?? throw new NotFoundException($"Appointment '{appointmentId}' was not found.");

        ValidateAttachmentRequest(request);

        var attachment = AppointmentAttachment.Create(
            tenantId,
            appointmentId,
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

    private static AttachmentMetadataResponse ToResponse(AppointmentAttachment a) => new()
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
