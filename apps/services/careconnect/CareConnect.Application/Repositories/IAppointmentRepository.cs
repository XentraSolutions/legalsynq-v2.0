using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IAppointmentRepository
{
    Task AddAsync(Appointment appointment, CancellationToken ct = default);
    Task<Appointment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(List<Appointment> Items, int TotalCount)> SearchAsync(Guid tenantId, Guid? referralId, Guid? providerId, string? status, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);
}
