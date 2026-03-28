using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly CareConnectDbContext _db;

    public AppointmentRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task SaveBookingAsync(AppointmentSlot slot, Appointment appointment, CancellationToken ct = default)
    {
        _db.AppointmentSlots.Update(slot);
        await _db.Appointments.AddAsync(appointment, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveStatusUpdateAsync(Appointment appointment, AppointmentStatusHistory? history, CancellationToken ct = default)
    {
        _db.Appointments.Update(appointment);
        if (history is not null)
            await _db.AppointmentStatusHistories.AddAsync(history, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveCancellationAsync(Appointment appointment, AppointmentSlot? slot, AppointmentStatusHistory history, CancellationToken ct = default)
    {
        _db.Appointments.Update(appointment);
        if (slot is not null)
            _db.AppointmentSlots.Update(slot);
        await _db.AppointmentStatusHistories.AddAsync(history, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveRescheduleAsync(Appointment appointment, AppointmentSlot? oldSlot, AppointmentSlot newSlot, CancellationToken ct = default)
    {
        _db.Appointments.Update(appointment);
        if (oldSlot is not null)
            _db.AppointmentSlots.Update(oldSlot);
        _db.AppointmentSlots.Update(newSlot);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Appointment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Appointments
            .Where(a => a.TenantId == tenantId && a.Id == id)
            .Include(a => a.Provider)
            .Include(a => a.Facility)
            .Include(a => a.ServiceOffering)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<Appointment> Items, int TotalCount)> SearchAsync(
        Guid tenantId,
        Guid? referralId,
        Guid? providerId,
        string? status,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Appointments
            .Where(a => a.TenantId == tenantId)
            .Include(a => a.Provider)
            .Include(a => a.Facility)
            .Include(a => a.ServiceOffering)
            .AsQueryable();

        if (referralId.HasValue)
            query = query.Where(a => a.ReferralId == referralId.Value);

        if (providerId.HasValue)
            query = query.Where(a => a.ProviderId == providerId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        if (from.HasValue)
            query = query.Where(a => a.ScheduledStartAtUtc >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.ScheduledStartAtUtc <= to.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(a => a.ScheduledStartAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
