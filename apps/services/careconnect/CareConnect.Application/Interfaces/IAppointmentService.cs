using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IAppointmentService
{
    Task<PagedResponse<SlotResponse>> SearchSlotsAsync(Guid tenantId, SlotSearchParams query, CancellationToken ct = default);
    Task<AppointmentResponse> CreateAppointmentAsync(Guid tenantId, Guid? userId, CreateAppointmentRequest request, CancellationToken ct = default);
    Task<PagedResponse<AppointmentResponse>> SearchAppointmentsAsync(Guid tenantId, AppointmentSearchParams query, CancellationToken ct = default);
    Task<AppointmentResponse> GetAppointmentByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
