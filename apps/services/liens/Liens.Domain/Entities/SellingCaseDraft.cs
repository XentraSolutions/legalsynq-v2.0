using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

/// <summary>
/// Seller-scoped case intake data captured before a plaintiff is supplied to create
/// the canonical case. A draft is finalized once and retains the resulting case id
/// to make the plaintiff submission safely retryable.
/// </summary>
public sealed class SellingCaseDraft : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrgId { get; private set; }

    public string CaseStatus { get; private set; } = string.Empty;
    public string? AccidentTypeId { get; private set; }
    public string? AccidentState { get; private set; }
    public DateOnly? DateOfLoss { get; private set; }
    public Guid? HandlingLawFirmCompanyId { get; private set; }
    public Guid? CaseManagerContactPersonId { get; private set; }
    public string? CaseTrackingNotes { get; private set; }

    public Guid? CaseId { get; private set; }
    public DateTime? FinalizedAtUtc { get; private set; }
    public Guid ConcurrencyToken { get; private set; }

    private SellingCaseDraft() { }

    public static SellingCaseDraft Create(
        Guid tenantId,
        Guid orgId,
        string caseStatus,
        Guid createdByUserId,
        string? accidentTypeId = null,
        string? accidentState = null,
        DateOnly? dateOfLoss = null,
        Guid? handlingLawFirmCompanyId = null,
        Guid? caseManagerContactPersonId = null,
        string? caseTrackingNotes = null)
    {
        RequireId(tenantId, nameof(tenantId));
        RequireId(orgId, nameof(orgId));
        RequireId(createdByUserId, nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(caseStatus);

        var now = DateTime.UtcNow;
        return new SellingCaseDraft
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            OrgId = orgId,
            CaseStatus = caseStatus.Trim(),
            AccidentTypeId = Trim(accidentTypeId),
            AccidentState = Trim(accidentState),
            DateOfLoss = dateOfLoss,
            HandlingLawFirmCompanyId = OptionalId(handlingLawFirmCompanyId, nameof(handlingLawFirmCompanyId)),
            CaseManagerContactPersonId = OptionalId(caseManagerContactPersonId, nameof(caseManagerContactPersonId)),
            CaseTrackingNotes = Trim(caseTrackingNotes),
            ConcurrencyToken = Guid.CreateVersion7(),
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void UpdateCaseInformation(
        string caseStatus,
        Guid updatedByUserId,
        string? accidentTypeId = null,
        string? accidentState = null,
        DateOnly? dateOfLoss = null,
        Guid? handlingLawFirmCompanyId = null,
        Guid? caseManagerContactPersonId = null,
        string? caseTrackingNotes = null)
    {
        EnsureUnfinalized();
        RequireId(updatedByUserId, nameof(updatedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(caseStatus);

        CaseStatus = caseStatus.Trim();
        AccidentTypeId = Trim(accidentTypeId);
        AccidentState = Trim(accidentState);
        DateOfLoss = dateOfLoss;
        HandlingLawFirmCompanyId = OptionalId(handlingLawFirmCompanyId, nameof(handlingLawFirmCompanyId));
        CaseManagerContactPersonId = OptionalId(caseManagerContactPersonId, nameof(caseManagerContactPersonId));
        CaseTrackingNotes = Trim(caseTrackingNotes);
        Touch(updatedByUserId);
    }

    public void Finalize(Guid caseId, Guid updatedByUserId)
    {
        EnsureUnfinalized();
        RequireId(caseId, nameof(caseId));
        RequireId(updatedByUserId, nameof(updatedByUserId));

        CaseId = caseId;
        FinalizedAtUtc = DateTime.UtcNow;
        Touch(updatedByUserId);
    }

    private void EnsureUnfinalized()
    {
        if (CaseId.HasValue)
            throw new InvalidOperationException("The selling case draft has already been finalized.");
    }

    private void Touch(Guid updatedByUserId)
    {
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    private static Guid? OptionalId(Guid? id, string parameterName)
        => id == Guid.Empty
            ? throw new ArgumentException("Identifier cannot be empty.", parameterName)
            : id;

    private static void RequireId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Identifier is required.", parameterName);
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
