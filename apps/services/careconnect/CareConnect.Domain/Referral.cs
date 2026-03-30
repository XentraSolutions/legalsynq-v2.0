using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class Referral : AuditableEntity
{
    public static class ValidStatuses
    {
        public const string New       = "New";
        public const string Received  = "Received";
        public const string Contacted = "Contacted";
        public const string Scheduled = "Scheduled";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        public static readonly IReadOnlyList<string> All =
            new[] { New, Received, Contacted, Scheduled, Completed, Cancelled };
    }

    public static class ValidUrgencies
    {
        public const string Low       = "Low";
        public const string Normal    = "Normal";
        public const string Urgent    = "Urgent";
        public const string Emergency = "Emergency";

        public static readonly IReadOnlyList<string> All =
            new[] { Low, Normal, Urgent, Emergency };
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    // ── Multi-org workflow participants ──────────────────────────────────
    public Guid? ReferringOrganizationId { get; private set; }
    public Guid? ReceivingOrganizationId { get; private set; }

    // ── Subject party (first-class client record) ────────────────────────
    public Guid? SubjectPartyId { get; private set; }
    public string? SubjectNameSnapshot { get; private set; }
    public DateOnly? SubjectDobSnapshot { get; private set; }

    // ── Provider routing ─────────────────────────────────────────────────
    public Guid ProviderId { get; private set; }

    // Phase 5: explicit relationship context linking referrer ↔ receiver orgs
    public Guid? OrganizationRelationshipId { get; private set; }

    // ── Legacy inline client fields (kept during migration window) ────────
    public string ClientFirstName { get; private set; } = string.Empty;
    public string ClientLastName { get; private set; } = string.Empty;
    public DateTime? ClientDob { get; private set; }
    public string ClientPhone { get; private set; } = string.Empty;
    public string ClientEmail { get; private set; } = string.Empty;

    // ── Referral detail ──────────────────────────────────────────────────
    public string? CaseNumber { get; private set; }
    public string RequestedService { get; private set; } = string.Empty;
    public string Urgency { get; private set; } = string.Empty;
    public string Status { get; private set; } = ValidStatuses.New;
    public string? Notes { get; private set; }

    public Provider? Provider { get; private set; }
    public Party? SubjectParty { get; private set; }

    private Referral() { }

    /// <summary>
    /// Create a referral with full multi-org context.
    /// If both referringOrganizationId and receivingOrganizationId are known and a matching
    /// OrganizationRelationship exists in Identity, pass organizationRelationshipId to link
    /// this referral to the formal relationship graph.
    /// </summary>
    public static Referral Create(
        Guid tenantId,
        Guid? referringOrganizationId,
        Guid? receivingOrganizationId,
        Guid providerId,
        Guid? subjectPartyId,
        string? subjectNameSnapshot,
        DateOnly? subjectDobSnapshot,
        string clientFirstName,
        string clientLastName,
        DateTime? clientDob,
        string clientPhone,
        string clientEmail,
        string? caseNumber,
        string requestedService,
        string urgency,
        string? notes,
        Guid? createdByUserId,
        Guid? organizationRelationshipId = null)
    {
        var now = DateTime.UtcNow;
        return new Referral
        {
            Id                       = Guid.NewGuid(),
            TenantId                 = tenantId,
            ReferringOrganizationId  = referringOrganizationId,
            ReceivingOrganizationId  = receivingOrganizationId,
            ProviderId               = providerId,
            OrganizationRelationshipId = organizationRelationshipId,
            SubjectPartyId           = subjectPartyId,
            SubjectNameSnapshot      = subjectNameSnapshot?.Trim(),
            SubjectDobSnapshot       = subjectDobSnapshot,
            ClientFirstName          = clientFirstName.Trim(),
            ClientLastName           = clientLastName.Trim(),
            ClientDob                = clientDob,
            ClientPhone              = clientPhone.Trim(),
            ClientEmail              = clientEmail.Trim(),
            CaseNumber               = caseNumber?.Trim(),
            RequestedService         = requestedService.Trim(),
            Urgency                  = urgency,
            Status                   = ValidStatuses.New,
            Notes                    = notes?.Trim(),
            CreatedByUserId          = createdByUserId,
            UpdatedByUserId          = createdByUserId,
            CreatedAtUtc             = now,
            UpdatedAtUtc             = now
        };
    }

    /// <summary>
    /// Phase C: link this referral to a formal OrganizationRelationship after creation.
    /// Used when the relationship is resolved asynchronously or after an import/backfill.
    /// </summary>
    public void SetOrganizationRelationshipId(Guid organizationRelationshipId)
    {
        OrganizationRelationshipId = organizationRelationshipId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Update(string requestedService, string urgency, string status, string? notes, Guid? updatedByUserId)
    {
        RequestedService = requestedService.Trim();
        Urgency          = urgency;
        Status           = status;
        Notes            = notes?.Trim();
        UpdatedByUserId  = updatedByUserId;
        UpdatedAtUtc     = DateTime.UtcNow;
    }

    public void LinkParty(Guid partyId, string nameSnapshot, DateOnly? dobSnapshot)
    {
        SubjectPartyId      = partyId;
        SubjectNameSnapshot = nameSnapshot;
        SubjectDobSnapshot  = dobSnapshot;
        UpdatedAtUtc        = DateTime.UtcNow;
    }
}
