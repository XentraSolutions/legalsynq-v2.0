using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class Case : AuditableEntity
{
    public Guid Id               { get; private set; }
    public Guid TenantId         { get; private set; }
    public Guid OrgId            { get; private set; }

    public string CaseNumber     { get; private set; } = string.Empty;
    public string? ExternalReference { get; private set; }
    public string? Title         { get; private set; }

    public string ClientFirstName { get; private set; } = string.Empty;
    public string ClientLastName  { get; private set; } = string.Empty;
    public DateOnly? ClientDob    { get; private set; }
    public string? ClientPhone    { get; private set; }
    public string? ClientEmail    { get; private set; }
    public string? ClientAddress  { get; private set; }
    public string? ClientAddressLine1 { get; private set; }
    public string? ClientCity { get; private set; }
    public string? ClientState { get; private set; }
    public string? ClientPostalCode { get; private set; }

    public string Status          { get; private set; } = CaseStatus.PreDemand;
    public DateOnly? DateOfIncident { get; private set; }
    public DateTime? OpenedAtUtc   { get; private set; }
    public DateTime? ClosedAtUtc   { get; private set; }

    public string? InsuranceCarrier { get; private set; }
    public string? PolicyNumber     { get; private set; }
    public string? ClaimNumber      { get; private set; }

    public decimal? DemandAmount     { get; private set; }
    public decimal? SettlementAmount { get; private set; }

    public string? Description { get; private set; }
    public string? Notes       { get; private set; }
    public string? IncidentState { get; private set; }
    public string? CurrentMedicalStatus { get; private set; }
    public DateOnly? TrackingFollowUpDate { get; private set; }
    public bool? MinorComp { get; private set; }
    public bool? CaseDropped { get; private set; }
    public string? ImportedCreatedByName { get; private set; }
    public Guid? HandlingLawFirmCompanyId { get; private set; }
    public Guid? CaseManagerContactPersonId { get; private set; }
    public Guid? AttorneyContactPersonId { get; private set; }

    private Case() { }

    public void LinkCanonicalCaseParties(Guid? lawFirmCompanyId, Guid? caseManagerContactPersonId)
    {
        if (lawFirmCompanyId == Guid.Empty) throw new ArgumentException("Canonical law firm id cannot be empty.", nameof(lawFirmCompanyId));
        if (caseManagerContactPersonId == Guid.Empty) throw new ArgumentException("Canonical case manager id cannot be empty.", nameof(caseManagerContactPersonId));
        HandlingLawFirmCompanyId = lawFirmCompanyId;
        CaseManagerContactPersonId = caseManagerContactPersonId;
    }

    public void SetCanonicalCaseParties(
        Guid? lawFirmCompanyId,
        Guid? caseManagerContactPersonId,
        Guid updatedByUserId)
    {
        LinkCanonicalCaseParties(lawFirmCompanyId, caseManagerContactPersonId);
        Touch(updatedByUserId);
    }

    public void ReassignCanonicalCompany(Guid sourceCompanyId, Guid targetCompanyId, Guid updatedByUserId)
    {
        ValidateReassignment(sourceCompanyId, targetCompanyId, updatedByUserId);
        if (HandlingLawFirmCompanyId != sourceCompanyId) return;
        HandlingLawFirmCompanyId = targetCompanyId;
        Touch(updatedByUserId);
    }

    public void ReassignCanonicalContactPerson(
        Guid sourceContactPersonId,
        Guid targetContactPersonId,
        Guid sourceCompanyId,
        Guid targetCompanyId,
        Guid updatedByUserId)
    {
        ValidateReassignment(sourceContactPersonId, targetContactPersonId, updatedByUserId);
        if (sourceCompanyId == Guid.Empty) throw new ArgumentException("Source company id is required.", nameof(sourceCompanyId));
        if (targetCompanyId == Guid.Empty) throw new ArgumentException("Target company id is required.", nameof(targetCompanyId));
        if (CaseManagerContactPersonId != sourceContactPersonId) return;
        CaseManagerContactPersonId = targetContactPersonId;
        if (HandlingLawFirmCompanyId == sourceCompanyId)
            HandlingLawFirmCompanyId = targetCompanyId;
        Touch(updatedByUserId);
    }

    private static void ValidateReassignment(Guid sourceId, Guid targetId, Guid updatedByUserId)
    {
        if (sourceId == Guid.Empty) throw new ArgumentException("Source id is required.", nameof(sourceId));
        if (targetId == Guid.Empty) throw new ArgumentException("Target id is required.", nameof(targetId));
        if (sourceId == targetId) throw new ArgumentException("Source and target ids must differ.", nameof(targetId));
        if (updatedByUserId == Guid.Empty) throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
    }

    private void Touch(Guid updatedByUserId)
    {
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static Case Create(
        Guid tenantId,
        Guid orgId,
        string caseNumber,
        string clientFirstName,
        string clientLastName,
        Guid createdByUserId,
        string? externalReference = null,
        string? title = null,
        DateOnly? clientDob = null,
        string? clientPhone = null,
        string? clientEmail = null,
        string? clientAddress = null,
        DateOnly? dateOfIncident = null,
        string? insuranceCarrier = null,
        string? policyNumber = null,
        string? claimNumber = null,
        string? description = null,
        string? notes = null,
        string? clientAddressLine1 = null,
        string? clientCity = null,
        string? clientState = null,
        string? clientPostalCode = null,
        string? incidentState = null,
        string? currentMedicalStatus = null,
        DateOnly? trackingFollowUpDate = null,
        bool? minorComp = null,
        bool? caseDropped = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(caseNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientFirstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientLastName);

        var now = DateTime.UtcNow;
        return new Case
        {
            Id                = Guid.CreateVersion7(),
            TenantId          = tenantId,
            OrgId             = orgId,
            CaseNumber        = caseNumber.Trim(),
            ExternalReference = externalReference?.Trim(),
            Title             = title?.Trim(),
            ClientFirstName   = clientFirstName.Trim(),
            ClientLastName    = clientLastName.Trim(),
            ClientDob         = clientDob,
            ClientPhone       = clientPhone?.Trim(),
            ClientEmail       = clientEmail?.Trim(),
            ClientAddress     = clientAddress?.Trim(),
            ClientAddressLine1 = clientAddressLine1?.Trim(),
            ClientCity        = clientCity?.Trim(),
            ClientState       = clientState?.Trim(),
            ClientPostalCode  = clientPostalCode?.Trim(),
            Status            = CaseStatus.PreDemand,
            DateOfIncident    = dateOfIncident,
            IncidentState     = incidentState?.Trim(),
            CurrentMedicalStatus = currentMedicalStatus?.Trim(),
            TrackingFollowUpDate = trackingFollowUpDate,
            MinorComp         = minorComp,
            CaseDropped       = caseDropped,
            OpenedAtUtc       = now,
            InsuranceCarrier  = insuranceCarrier?.Trim(),
            PolicyNumber      = policyNumber?.Trim(),
            ClaimNumber       = claimNumber?.Trim(),
            Description       = description?.Trim(),
            Notes             = notes?.Trim(),
            CreatedByUserId   = createdByUserId,
            UpdatedByUserId   = createdByUserId,
            CreatedAtUtc      = now,
            UpdatedAtUtc      = now,
        };
    }

    public void Update(
        string clientFirstName,
        string clientLastName,
        Guid updatedByUserId,
        string? title = null,
        string? externalReference = null,
        DateOnly? clientDob = null,
        string? clientPhone = null,
        string? clientEmail = null,
        string? clientAddress = null,
        DateOnly? dateOfIncident = null,
        string? insuranceCarrier = null,
        string? policyNumber = null,
        string? claimNumber = null,
        string? description = null,
        string? notes = null,
        string? clientAddressLine1 = null,
        string? clientCity = null,
        string? clientState = null,
        string? clientPostalCode = null,
        string? incidentState = null,
        string? currentMedicalStatus = null,
        DateOnly? trackingFollowUpDate = null,
        bool? minorComp = null,
        bool? caseDropped = null,
        Guid? attorneyContactPersonId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientFirstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientLastName);

        ClientFirstName   = clientFirstName.Trim();
        ClientLastName    = clientLastName.Trim();
        Title             = title?.Trim();
        ExternalReference = externalReference?.Trim();
        ClientDob         = clientDob;
        ClientPhone       = clientPhone?.Trim();
        ClientEmail       = clientEmail?.Trim();
        ClientAddress     = clientAddress?.Trim();
        ClientAddressLine1 = clientAddressLine1?.Trim();
        ClientCity        = clientCity?.Trim();
        ClientState       = clientState?.Trim();
        ClientPostalCode  = clientPostalCode?.Trim();
        DateOfIncident    = dateOfIncident;
        IncidentState     = incidentState?.Trim();
        CurrentMedicalStatus = currentMedicalStatus?.Trim();
        TrackingFollowUpDate = trackingFollowUpDate;
        MinorComp         = minorComp;
        CaseDropped       = caseDropped;
        AttorneyContactPersonId = attorneyContactPersonId;
        InsuranceCarrier  = insuranceCarrier?.Trim();
        PolicyNumber      = policyNumber?.Trim();
        ClaimNumber       = claimNumber?.Trim();
        Description       = description?.Trim();
        Notes             = notes?.Trim();
        UpdatedByUserId   = updatedByUserId;
        UpdatedAtUtc      = DateTime.UtcNow;
    }

    public void TransitionStatus(string newStatus, Guid updatedByUserId)
    {
        if (!CaseStatus.All.Contains(newStatus))
            throw new ArgumentException($"Invalid case status: '{newStatus}'.");

        Status          = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;

        if (newStatus == CaseStatus.Closed)
            ClosedAtUtc = DateTime.UtcNow;
    }

    public void SetDemandAmount(decimal amount, Guid updatedByUserId)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Demand amount cannot be negative.");

        DemandAmount    = amount;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void SetSettlementAmount(decimal amount, Guid updatedByUserId)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Settlement amount cannot be negative.");

        SettlementAmount = amount;
        UpdatedByUserId  = updatedByUserId;
        UpdatedAtUtc     = DateTime.UtcNow;
    }

    public void ReassignLawFirm(Guid lawFirmOrgId, Guid updatedByUserId)
    {
        if (lawFirmOrgId == Guid.Empty)
            throw new ArgumentException("Law firm organization id is required.", nameof(lawFirmOrgId));

        OrgId          = lawFirmOrgId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void ReassignCaseManager(Guid caseManagerId, Guid updatedByUserId)
    {
        if (caseManagerId == Guid.Empty)
            throw new ArgumentException("Case manager id is required.", nameof(caseManagerId));

        var metadata = ParseMetadata(Notes);
        metadata["caseManagerId"] = caseManagerId.ToString();
        Notes = SerializeMetadata(metadata);

        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ApplyScheduledLawFirmSwitch(string? serializedNotes, Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        Notes = string.IsNullOrWhiteSpace(serializedNotes) ? null : serializedNotes.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkForDeletion(Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static Dictionary<string, string> ParseMetadata(string? value)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (var segment in value.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = segment.IndexOf('=');
            if (idx <= 0)
                continue;

            var key = segment[..idx].Trim();
            var itemValue = segment[(idx + 1)..].Trim();
            if (key.Length > 0)
                result[key] = itemValue;
        }

        return result;
    }

    private static string? SerializeMetadata(Dictionary<string, string> metadata)
    {
        if (metadata.Count == 0)
            return null;

        return string.Join("; ", metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }
}
