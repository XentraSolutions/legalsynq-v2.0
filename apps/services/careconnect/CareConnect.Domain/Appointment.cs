using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class Appointment : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ReferralId { get; private set; }

    // ── Multi-org workflow participants (denormalized from Referral at create time) ──
    public Guid? ReferringOrganizationId { get; private set; }
    public Guid? ReceivingOrganizationId { get; private set; }
    public Guid? SubjectPartyId { get; private set; }

    // Phase 5: explicit relationship context (denormalized from Referral at create time)
    public Guid? OrganizationRelationshipId { get; private set; }

    public Guid ProviderId { get; private set; }
    public Guid FacilityId { get; private set; }
    public Guid? ServiceOfferingId { get; private set; }
    public Guid? AppointmentSlotId { get; private set; }
    public DateTime ScheduledStartAtUtc { get; private set; }
    public DateTime ScheduledEndAtUtc { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    public Referral? Referral { get; private set; }
    public Provider? Provider { get; private set; }
    public Facility? Facility { get; private set; }
    public ServiceOffering? ServiceOffering { get; private set; }
    public AppointmentSlot? AppointmentSlot { get; private set; }

    private Appointment() { }

    public void UpdateStatus(string newStatus, Guid? updatedByUserId)
    {
        Status = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateNotes(string? notes, Guid? updatedByUserId)
    {
        Notes = notes?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reschedule(AppointmentSlot newSlot, string? notes, Guid? updatedByUserId)
    {
        AppointmentSlotId = newSlot.Id;
        ProviderId = newSlot.ProviderId;
        FacilityId = newSlot.FacilityId;
        ServiceOfferingId = newSlot.ServiceOfferingId;
        ScheduledStartAtUtc = newSlot.StartAtUtc;
        ScheduledEndAtUtc = newSlot.EndAtUtc;
        if (notes is not null)
            Notes = notes.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static Appointment Create(
        Guid tenantId,
        Guid referralId,
        Guid providerId,
        Guid facilityId,
        Guid? serviceOfferingId,
        Guid? appointmentSlotId,
        DateTime scheduledStartAtUtc,
        DateTime scheduledEndAtUtc,
        string? notes,
        Guid? createdByUserId)
    {
        return new Appointment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReferralId = referralId,
            ProviderId = providerId,
            FacilityId = facilityId,
            ServiceOfferingId = serviceOfferingId,
            AppointmentSlotId = appointmentSlotId,
            ScheduledStartAtUtc = scheduledStartAtUtc,
            ScheduledEndAtUtc = scheduledEndAtUtc,
            Status = AppointmentStatus.Scheduled,
            Notes = notes?.Trim(),
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
