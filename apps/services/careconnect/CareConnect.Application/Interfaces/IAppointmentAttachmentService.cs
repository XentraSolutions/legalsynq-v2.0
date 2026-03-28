using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IAppointmentAttachmentService
{
    Task<List<AttachmentMetadataResponse>> GetByAppointmentAsync(Guid tenantId, Guid appointmentId, CancellationToken ct = default);
    Task<AttachmentMetadataResponse> CreateAsync(Guid tenantId, Guid appointmentId, Guid? userId, CreateAttachmentMetadataRequest request, CancellationToken ct = default);
}
