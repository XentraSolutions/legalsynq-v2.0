using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IAppointmentSlotRepository _slots;
    private readonly IAppointmentRepository _appointments;
    private readonly IAppointmentStatusHistoryRepository _history;
    private readonly IReferralRepository _referrals;

    public AppointmentService(
        IAppointmentSlotRepository slots,
        IAppointmentRepository appointments,
        IAppointmentStatusHistoryRepository history,
        IReferralRepository referrals)
    {
        _slots = slots;
        _appointments = appointments;
        _history = history;
        _referrals = referrals;
    }

    public async Task<PagedResponse<SlotResponse>> SearchSlotsAsync(
        Guid tenantId,
        SlotSearchParams query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Min(100, Math.Max(1, query.PageSize ?? 20));

        var (items, total) = await _slots.SearchAsync(
            tenantId,
            query.ProviderId,
            query.FacilityId,
            query.ServiceOfferingId,
            query.From,
            query.To,
            query.Status,
            page,
            pageSize,
            ct);

        return new PagedResponse<SlotResponse>
        {
            Items = items.Select(ToSlotResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<AppointmentResponse> CreateAppointmentAsync(
        Guid tenantId,
        Guid? userId,
        CreateAppointmentRequest request,
        CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.ReferralId == Guid.Empty)
            errors["referralId"] = new[] { "ReferralId is required." };

        if (request.AppointmentSlotId == Guid.Empty)
            errors["appointmentSlotId"] = new[] { "AppointmentSlotId is required." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);

        _ = await _referrals.GetByIdAsync(tenantId, request.ReferralId, ct)
            ?? throw new NotFoundException($"Referral '{request.ReferralId}' was not found.");

        var slot = await _slots.GetByIdAsync(tenantId, request.AppointmentSlotId, ct)
            ?? throw new NotFoundException($"Appointment slot '{request.AppointmentSlotId}' was not found.");

        if (slot.Status != SlotStatus.Open)
            throw new ValidationException("One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["appointmentSlotId"] = new[] { "Slot is not available for booking." }
                });

        if (slot.ReservedCount >= slot.Capacity)
            throw new ValidationException("One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["appointmentSlotId"] = new[] { "Slot has no remaining capacity." }
                });

        slot.Reserve(userId);

        var appointment = Appointment.Create(
            tenantId,
            request.ReferralId,
            slot.ProviderId,
            slot.FacilityId,
            slot.ServiceOfferingId,
            slot.Id,
            slot.StartAtUtc,
            slot.EndAtUtc,
            request.Notes,
            userId);

        await _appointments.SaveBookingAsync(slot, appointment, ct);

        var loaded = await _appointments.GetByIdAsync(tenantId, appointment.Id, ct);
        return ToAppointmentResponse(loaded!);
    }

    public async Task<PagedResponse<AppointmentResponse>> SearchAppointmentsAsync(
        Guid tenantId,
        AppointmentSearchParams query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Min(100, Math.Max(1, query.PageSize ?? 20));

        var (items, total) = await _appointments.SearchAsync(
            tenantId,
            query.ReferralId,
            query.ProviderId,
            query.Status,
            query.From,
            query.To,
            page,
            pageSize,
            ct);

        return new PagedResponse<AppointmentResponse>
        {
            Items = items.Select(ToAppointmentResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<AppointmentResponse> GetAppointmentByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        return ToAppointmentResponse(appointment);
    }

    public async Task<AppointmentResponse> UpdateAppointmentAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        UpdateAppointmentRequest request,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        AppointmentStatusHistory? history = null;

        if (request.Status is not null && request.Status != appointment.Status)
        {
            AppointmentWorkflowRules.ValidateStatus(request.Status);
            AppointmentWorkflowRules.ValidateTransition(appointment.Status, request.Status);

            var oldStatus = appointment.Status;
            appointment.UpdateStatus(request.Status, userId);

            history = AppointmentStatusHistory.Create(
                appointment.Id,
                tenantId,
                oldStatus,
                request.Status,
                userId,
                request.Notes);
        }

        if (request.Notes is not null)
            appointment.UpdateNotes(request.Notes, userId);

        await _appointments.SaveStatusUpdateAsync(appointment, history, ct);

        var loaded = await _appointments.GetByIdAsync(tenantId, appointment.Id, ct);
        return ToAppointmentResponse(loaded!);
    }

    public async Task<AppointmentResponse> CancelAppointmentAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        CancelAppointmentRequest request,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        if (AppointmentWorkflowRules.IsTerminal(appointment.Status))
            throw new ValidationException(
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["status"] = new[] { $"Cannot cancel an appointment with terminal status '{appointment.Status}'." }
                });

        AppointmentWorkflowRules.ValidateTransition(appointment.Status, AppointmentStatus.Cancelled);

        AppointmentSlot? slot = null;
        if (appointment.AppointmentSlotId.HasValue)
        {
            slot = await _slots.GetByIdAsync(tenantId, appointment.AppointmentSlotId.Value, ct);
            slot?.Release(userId);
        }

        var oldStatus = appointment.Status;
        appointment.UpdateStatus(AppointmentStatus.Cancelled, userId);

        var history = AppointmentStatusHistory.Create(
            appointment.Id,
            tenantId,
            oldStatus,
            AppointmentStatus.Cancelled,
            userId,
            request.Notes);

        await _appointments.SaveCancellationAsync(appointment, slot, history, ct);

        var loaded = await _appointments.GetByIdAsync(tenantId, appointment.Id, ct);
        return ToAppointmentResponse(loaded!);
    }

    public async Task<AppointmentResponse> RescheduleAppointmentAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        RescheduleAppointmentRequest request,
        CancellationToken ct = default)
    {
        if (request.NewAppointmentSlotId == Guid.Empty)
            throw new ValidationException(
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["newAppointmentSlotId"] = new[] { "NewAppointmentSlotId is required." }
                });

        var appointment = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        if (!AppointmentWorkflowRules.IsReschedulable(appointment.Status))
            throw new ValidationException(
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["status"] = new[] { $"Cannot reschedule an appointment with status '{appointment.Status}'." }
                });

        if (appointment.AppointmentSlotId == request.NewAppointmentSlotId)
            throw new ValidationException(
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["newAppointmentSlotId"] = new[] { "New slot must be different from the current slot." }
                });

        var newSlot = await _slots.GetByIdAsync(tenantId, request.NewAppointmentSlotId, ct)
            ?? throw new NotFoundException($"Appointment slot '{request.NewAppointmentSlotId}' was not found.");

        if (newSlot.Status != SlotStatus.Open)
            throw new ValidationException(
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["newAppointmentSlotId"] = new[] { "The target slot is not available for booking." }
                });

        if (newSlot.ReservedCount >= newSlot.Capacity)
            throw new ValidationException(
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["newAppointmentSlotId"] = new[] { "The target slot has no remaining capacity." }
                });

        AppointmentSlot? oldSlot = null;
        if (appointment.AppointmentSlotId.HasValue)
        {
            oldSlot = await _slots.GetByIdAsync(tenantId, appointment.AppointmentSlotId.Value, ct);
            oldSlot?.Release(userId);
        }

        newSlot.Reserve(userId);
        appointment.Reschedule(newSlot, request.Notes, userId);

        await _appointments.SaveRescheduleAsync(appointment, oldSlot, newSlot, ct);

        var loaded = await _appointments.GetByIdAsync(tenantId, appointment.Id, ct);
        return ToAppointmentResponse(loaded!);
    }

    public async Task<List<AppointmentStatusHistoryResponse>> GetAppointmentHistoryAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
    {
        _ = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        var rows = await _history.GetByAppointmentIdAsync(tenantId, id, ct);

        return rows.Select(h => new AppointmentStatusHistoryResponse
        {
            Id = h.Id,
            AppointmentId = h.AppointmentId,
            OldStatus = h.OldStatus,
            NewStatus = h.NewStatus,
            ChangedByUserId = h.ChangedByUserId,
            ChangedAtUtc = h.ChangedAtUtc,
            Notes = h.Notes
        }).ToList();
    }

    private static SlotResponse ToSlotResponse(AppointmentSlot s) => new()
    {
        Id = s.Id,
        TenantId = s.TenantId,
        ProviderId = s.ProviderId,
        ProviderName = s.Provider?.Name ?? string.Empty,
        FacilityId = s.FacilityId,
        FacilityName = s.Facility?.Name ?? string.Empty,
        ServiceOfferingId = s.ServiceOfferingId,
        ServiceOfferingName = s.ServiceOffering?.Name,
        StartAtUtc = s.StartAtUtc,
        EndAtUtc = s.EndAtUtc,
        Capacity = s.Capacity,
        ReservedCount = s.ReservedCount,
        AvailableCount = s.Capacity - s.ReservedCount,
        Status = s.Status
    };

    private static AppointmentResponse ToAppointmentResponse(Appointment a) => new()
    {
        Id = a.Id,
        TenantId = a.TenantId,
        ReferralId = a.ReferralId,
        ProviderId = a.ProviderId,
        ProviderName = a.Provider?.Name ?? string.Empty,
        FacilityId = a.FacilityId,
        FacilityName = a.Facility?.Name ?? string.Empty,
        ServiceOfferingId = a.ServiceOfferingId,
        ServiceOfferingName = a.ServiceOffering?.Name,
        AppointmentSlotId = a.AppointmentSlotId,
        ScheduledStartAtUtc = a.ScheduledStartAtUtc,
        ScheduledEndAtUtc = a.ScheduledEndAtUtc,
        Status = a.Status,
        Notes = a.Notes,
        CreatedAtUtc = a.CreatedAtUtc,
        UpdatedAtUtc = a.UpdatedAtUtc
    };
}
