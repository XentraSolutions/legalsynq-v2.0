using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class Lien : AuditableEntity
{
    public Guid Id               { get; private set; }
    public Guid TenantId         { get; private set; }
    public Guid OrgId            { get; private set; }

    public string LienNumber     { get; private set; } = string.Empty;
    public string? ExternalReference { get; private set; }

    public string LienType       { get; private set; } = Enums.LienType.MedicalLien;
    public string Status         { get; private set; } = LienStatus.Draft;

    public Guid? CaseId          { get; private set; }
    public Guid? SellingCaseId   { get; private set; }
    public DateTime? MovedToManagementAtUtc { get; private set; }
    public Guid? FacilityId      { get; private set; }
    public Guid? SubjectPartyId  { get; private set; }

    public string? SubjectFirstName { get; private set; }
    public string? SubjectLastName  { get; private set; }
    public bool IsConfidential      { get; private set; }

    public decimal OriginalAmount   { get; private set; }
    public decimal? CurrentBalance  { get; private set; }
    public decimal? OfferPrice      { get; private set; }
    public decimal? PurchasePrice   { get; private set; }
    public decimal? PayoffAmount    { get; private set; }

    public string? Jurisdiction  { get; private set; }
    public string? Description   { get; private set; }
    public string? Notes         { get; private set; }
    public string? BuyerMessage  { get; private set; }

    public DateOnly? IncidentDate { get; private set; }
    public DateOnly? PurchaseDate { get; private set; }
    public DateOnly? ReceivableDueDate { get; private set; }
    public DateOnly? InitialServiceDate { get; private set; }
    public DateOnly? EndServiceDate { get; private set; }
    public string? IsBulk { get; private set; }
    public string? IsServicing { get; private set; }
    // Preserves a legacy text value that cannot be safely resolved to a V2 user.
    public string? ImportedCreatedByName { get; private set; }
    public DateTime? OpenedAtUtc  { get; private set; }
    public DateTime? ClosedAtUtc  { get; private set; }

    public Guid? SellingOrgId  { get; private set; }
    public Guid? BuyingOrgId   { get; private set; }
    public Guid? HoldingOrgId  { get; private set; }

    public string? SellerStatus { get; private set; }
    public string? ListingVisibility { get; private set; }
    public Guid? FundingCompanyId { get; private set; }
    public Guid? FundingCompanyContactId { get; private set; }
    public Guid? FundingCompanyCompanyId { get; private set; }
    public Guid? FundingCompanyContactPersonId { get; private set; }
    public Guid? MedicalProviderCompanyId { get; private set; }
    public Guid? MedicalFacilityCompanyId { get; private set; }
    public decimal? AskAmount { get; private set; }
    public decimal? HighestBidAmount { get; private set; }
    public DateTime? SubmittedForSaleAtUtc { get; private set; }
    public DateTime? SoldAtUtc { get; private set; }
    public DateTime? WithdrawnAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public string? ArchivedReason { get; private set; }

    private Lien() { }

    public void LinkCanonicalSellingParties(
        Guid? fundingCompanyCompanyId,
        Guid? fundingCompanyContactPersonId,
        Guid? medicalProviderCompanyId,
        Guid? medicalFacilityCompanyId)
    {
        FundingCompanyCompanyId = NormalizeOptionalId(fundingCompanyCompanyId, nameof(fundingCompanyCompanyId));
        FundingCompanyContactPersonId = NormalizeOptionalId(fundingCompanyContactPersonId, nameof(fundingCompanyContactPersonId));
        MedicalProviderCompanyId = NormalizeOptionalId(medicalProviderCompanyId, nameof(medicalProviderCompanyId));
        MedicalFacilityCompanyId = NormalizeOptionalId(medicalFacilityCompanyId, nameof(medicalFacilityCompanyId));
    }

    public void SetSellingFundingReferences(
        Guid? legacyFundingCompanyId,
        Guid? legacyFundingCompanyContactId,
        Guid? fundingCompanyCompanyId,
        Guid? fundingCompanyContactPersonId,
        Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        legacyFundingCompanyId = NormalizeOptionalId(legacyFundingCompanyId, nameof(legacyFundingCompanyId));
        legacyFundingCompanyContactId = NormalizeOptionalId(legacyFundingCompanyContactId, nameof(legacyFundingCompanyContactId));
        fundingCompanyCompanyId = NormalizeOptionalId(fundingCompanyCompanyId, nameof(fundingCompanyCompanyId));
        fundingCompanyContactPersonId = NormalizeOptionalId(fundingCompanyContactPersonId, nameof(fundingCompanyContactPersonId));

        if (legacyFundingCompanyId.HasValue && fundingCompanyCompanyId.HasValue)
            throw new ArgumentException("Legacy and canonical funding-company references cannot both be assigned.");
        if (legacyFundingCompanyContactId.HasValue && !legacyFundingCompanyId.HasValue)
            throw new ArgumentException("A legacy funding-company contact requires a legacy funding company.", nameof(legacyFundingCompanyContactId));
        if (fundingCompanyContactPersonId.HasValue && !fundingCompanyCompanyId.HasValue)
            throw new ArgumentException("A canonical funding-company contact requires a canonical funding company.", nameof(fundingCompanyContactPersonId));

        FundingCompanyId = legacyFundingCompanyId;
        FundingCompanyContactId = legacyFundingCompanyContactId;
        FundingCompanyCompanyId = fundingCompanyCompanyId;
        FundingCompanyContactPersonId = fundingCompanyContactPersonId;
        if (legacyFundingCompanyId.HasValue || fundingCompanyCompanyId.HasValue)
            WithdrawnAtUtc = null;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ClearSellingFundingReferences(Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        FundingCompanyId = null;
        FundingCompanyContactId = null;
        FundingCompanyCompanyId = null;
        FundingCompanyContactPersonId = null;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetCanonicalMedicalProvider(Guid medicalProviderCompanyId, Guid updatedByUserId)
    {
        if (medicalProviderCompanyId == Guid.Empty)
            throw new ArgumentException("Medical provider company id is required.", nameof(medicalProviderCompanyId));
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        MedicalProviderCompanyId = medicalProviderCompanyId;
        Touch(updatedByUserId);
    }

    public void SetCanonicalMedicalFacility(Guid medicalFacilityCompanyId, Guid updatedByUserId)
    {
        if (medicalFacilityCompanyId == Guid.Empty)
            throw new ArgumentException("Medical facility company id is required.", nameof(medicalFacilityCompanyId));
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        MedicalFacilityCompanyId = medicalFacilityCompanyId;
        FacilityId = null;
        Touch(updatedByUserId);
    }

    public void SetSellingMedicalFacility(
        Guid? facilityId,
        Guid? medicalFacilityCompanyId,
        Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        FacilityId = NormalizeOptionalId(facilityId, nameof(facilityId));
        MedicalFacilityCompanyId = NormalizeOptionalId(medicalFacilityCompanyId, nameof(medicalFacilityCompanyId));
        Touch(updatedByUserId);
    }

    public void ReassignCanonicalCompany(Guid sourceCompanyId, Guid targetCompanyId, Guid updatedByUserId)
    {
        ValidateReassignment(sourceCompanyId, targetCompanyId, updatedByUserId);
        var changed = false;
        if (FundingCompanyCompanyId == sourceCompanyId)
        {
            FundingCompanyCompanyId = targetCompanyId;
            changed = true;
        }
        if (MedicalProviderCompanyId == sourceCompanyId)
        {
            MedicalProviderCompanyId = targetCompanyId;
            changed = true;
        }
        if (MedicalFacilityCompanyId == sourceCompanyId)
        {
            MedicalFacilityCompanyId = targetCompanyId;
            changed = true;
        }
        if (changed) Touch(updatedByUserId);
    }

    public void ReassignCanonicalContactPerson(
        Guid sourceContactPersonId,
        Guid targetContactPersonId,
        Guid targetCompanyId,
        Guid updatedByUserId)
    {
        ValidateReassignment(sourceContactPersonId, targetContactPersonId, updatedByUserId);
        if (targetCompanyId == Guid.Empty) throw new ArgumentException("Target company id is required.", nameof(targetCompanyId));
        if (FundingCompanyContactPersonId != sourceContactPersonId) return;
        FundingCompanyContactPersonId = targetContactPersonId;
        FundingCompanyCompanyId = targetCompanyId;
        Touch(updatedByUserId);
    }

    private static Guid? NormalizeOptionalId(Guid? id, string parameterName)
        => id == Guid.Empty ? throw new ArgumentException("Canonical id cannot be empty.", parameterName) : id;

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

    public void SetReceivableDueDate(DateOnly? receivableDueDate, Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        ReceivableDueDate = receivableDueDate;
        Touch(updatedByUserId);
    }

    public void SetPurchaseDate(DateOnly purchaseDate, Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        PurchaseDate = purchaseDate;
        Touch(updatedByUserId);
    }

    public static Lien Create(
        Guid tenantId,
        Guid orgId,
        string lienNumber,
        string lienType,
        decimal originalAmount,
        Guid createdByUserId,
        string? externalReference = null,
        Guid? caseId = null,
        Guid? facilityId = null,
        Guid? subjectPartyId = null,
        string? subjectFirstName = null,
        string? subjectLastName = null,
        bool isConfidential = false,
        string? jurisdiction = null,
        DateOnly? incidentDate = null,
        DateOnly? initialServiceDate = null,
        DateOnly? endServiceDate = null,
        string? isBulk = null,
        string? isServicing = null,
        string? description = null,
        string? notes = null,
        DateOnly? purchaseDate = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(lienNumber);

        if (!Enums.LienType.All.Contains(lienType))
            throw new ArgumentException($"Invalid lien type: '{lienType}'.");

        if (originalAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "Original amount cannot be negative.");

        var now = DateTime.UtcNow;
        return new Lien
        {
            Id                = Guid.CreateVersion7(),
            TenantId          = tenantId,
            OrgId             = orgId,
            LienNumber        = lienNumber.Trim(),
            ExternalReference = externalReference?.Trim(),
            LienType          = lienType,
            Status            = LienStatus.Draft,
            CaseId            = caseId,
            FacilityId        = facilityId,
            SubjectPartyId    = subjectPartyId,
            SubjectFirstName  = subjectFirstName?.Trim(),
            SubjectLastName   = subjectLastName?.Trim(),
            IsConfidential    = isConfidential,
            OriginalAmount    = originalAmount,
            CurrentBalance    = originalAmount,
            Jurisdiction      = jurisdiction?.Trim(),
            IncidentDate      = incidentDate,
            PurchaseDate      = purchaseDate,
            InitialServiceDate = initialServiceDate,
            EndServiceDate    = endServiceDate,
            IsBulk            = isBulk?.Trim(),
            IsServicing       = isServicing?.Trim(),
            Description       = description?.Trim(),
            Notes             = notes?.Trim(),
            OpenedAtUtc       = now,
            SellingOrgId      = orgId,
            SellerStatus      = SellingLienStatus.Pending,
            ListingVisibility = SellingListingVisibility.Private,
            CreatedByUserId   = createdByUserId,
            UpdatedByUserId   = createdByUserId,
            CreatedAtUtc      = now,
            UpdatedAtUtc      = now,
        };
    }

    public void Update(
        string lienType,
        decimal originalAmount,
        Guid updatedByUserId,
        string? externalReference = null,
        string? subjectFirstName = null,
        string? subjectLastName = null,
        bool? isConfidential = null,
        string? jurisdiction = null,
        DateOnly? incidentDate = null,
        DateOnly? initialServiceDate = null,
        DateOnly? endServiceDate = null,
        string? isBulk = null,
        string? isServicing = null,
        string? description = null,
        string? notes = null,
        DateOnly? purchaseDate = null,
        bool allowSettledServicingCorrection = false)
    {
        if (!Enums.LienType.All.Contains(lienType))
            throw new ArgumentException($"Invalid lien type: '{lienType}'.");

        if (originalAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "Original amount cannot be negative.");

        if (!LienStatus.Open.Contains(Status) &&
            !(allowSettledServicingCorrection && Status == LienStatus.Settled))
            throw new InvalidOperationException($"Cannot update a lien in terminal status '{Status}'.");

        LienType          = lienType;
        OriginalAmount    = originalAmount;
        ExternalReference = externalReference?.Trim();
        SubjectFirstName  = subjectFirstName?.Trim();
        SubjectLastName   = subjectLastName?.Trim();
        if (isConfidential.HasValue) IsConfidential = isConfidential.Value;
        Jurisdiction      = jurisdiction?.Trim();
        IncidentDate      = incidentDate;
        PurchaseDate      = purchaseDate;
        InitialServiceDate = initialServiceDate;
        EndServiceDate    = endServiceDate;
        IsBulk            = isBulk?.Trim();
        IsServicing       = isServicing?.Trim();
        Description       = description?.Trim();
        Notes             = notes?.Trim();
        UpdatedByUserId   = updatedByUserId;
        UpdatedAtUtc      = DateTime.UtcNow;
    }

    public void TransitionStatus(string newStatus, Guid updatedByUserId)
    {
        if (!LienStatus.All.Contains(newStatus))
            throw new ArgumentException($"Invalid lien status: '{newStatus}'.");

        if (!LienStatus.AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidOperationException($"Cannot transition from '{Status}' to '{newStatus}'.");

        Status          = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;

        if (LienStatus.Terminal.Contains(newStatus))
            ClosedAtUtc = DateTime.UtcNow;
    }

    public void SetLegacyMedicalStatus(string newStatus, Guid updatedByUserId)
    {
        newStatus = NormalizeLegacyMedicalStatus(newStatus);

        if (!LienStatus.All.Contains(newStatus))
            throw new ArgumentException($"Invalid lien status: '{newStatus}'.");

        if (string.Equals(Status, newStatus, StringComparison.Ordinal))
            return;

        Status          = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;

        if (LienStatus.Terminal.Contains(newStatus))
            ClosedAtUtc = DateTime.UtcNow;
        else
            ClosedAtUtc = null;
    }

    private static string NormalizeLegacyMedicalStatus(string newStatus)
    {
        var normalized = newStatus.Trim();

        return normalized.ToUpperInvariant() switch
        {
            "OPEN" => LienStatus.Active,
            "CLOSED" => LienStatus.Settled,
            "REJECTED" => LienStatus.Cancelled,
            _ => normalized,
        };
    }

    public void ListForSale(decimal offerPrice, Guid updatedByUserId, string? offerNotes = null)
    {
        if (Status != LienStatus.Draft)
            throw new InvalidOperationException($"Only draft liens can be listed for sale. Current status: '{Status}'.");

        if (offerPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(offerPrice), "Offer price must be positive.");

        OfferPrice      = offerPrice;
        AskAmount       = offerPrice;
        Status          = LienStatus.Offered;
        SellerStatus    = SellingLienStatus.SubmittedForSale;
        SubmittedForSaleAtUtc ??= DateTime.UtcNow;
        WithdrawnAtUtc  = null;
        Notes           = offerNotes?.Trim() ?? Notes;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Withdraw(Guid updatedByUserId)
    {
        if (Status != LienStatus.Offered && Status != LienStatus.Accepted && Status != LienStatus.UnderReview)
            throw new InvalidOperationException($"Only offered, accepted, or under-review liens can be withdrawn. Current status: '{Status}'.");

        Status          = LienStatus.Withdrawn;
        ClosedAtUtc     = DateTime.UtcNow;
        SellerStatus    = SellingLienStatus.Withdrawn;
        WithdrawnAtUtc  = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void ReturnToSellingPending(Guid updatedByUserId, bool recordWithdrawal = false)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        if (Status != LienStatus.Offered && Status != LienStatus.UnderReview)
            throw new InvalidOperationException($"Only offered or under-review liens can return to selling pending. Current status: '{Status}'.");

        Status = LienStatus.Draft;
        SellerStatus = SellingLienStatus.Pending;
        OfferPrice = null;
        HighestBidAmount = null;
        SubmittedForSaleAtUtc = null;
        ClosedAtUtc = null;
        WithdrawnAtUtc = recordWithdrawal ? DateTime.UtcNow : null;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MoveToInternalManagement(Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
        if (!CaseId.HasValue)
            throw new InvalidOperationException("A lien must be linked to a case before it can be moved to management.");
        if (SellerStatus is SellingLienStatus.Sold or SellingLienStatus.Archived ||
            Status is LienStatus.Sold or LienStatus.Settled)
            throw new InvalidOperationException("Sold, settled, or archived liens cannot be moved to management.");

        if (Status != LienStatus.Draft)
            throw new InvalidOperationException($"Lien status '{Status}' cannot be moved to management.");
        if (SellerStatus is not null && SellerStatus is not (
                SellingLienStatus.Pending or
                SellingLienStatus.Internal or
                SellingLienStatus.Approval or
                SellingLienStatus.PreparedForSale))
            throw new InvalidOperationException($"Seller status '{SellerStatus}' cannot be moved to management.");

        SellingCaseId ??= CaseId;
        SellerStatus = SellingLienStatus.Internal;
        MovedToManagementAtUtc ??= DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkSold(decimal purchasePrice, Guid buyingOrgId, Guid updatedByUserId)
    {
        if (Status != LienStatus.Offered && Status != LienStatus.Accepted && Status != LienStatus.UnderReview)
            throw new InvalidOperationException($"Only offered, accepted, or under-review liens can be sold. Current status: '{Status}'.");

        if (purchasePrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(purchasePrice), "Purchase price must be positive.");

        if (buyingOrgId == Guid.Empty)
            throw new ArgumentException("BuyingOrgId is required.", nameof(buyingOrgId));

        PurchasePrice   = purchasePrice;
        BuyingOrgId     = buyingOrgId;
        HoldingOrgId    = buyingOrgId;
        Status          = LienStatus.Sold;
        SellerStatus    = SellingLienStatus.Sold;
        SoldAtUtc       = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Activate(Guid updatedByUserId)
    {
        if (Status != LienStatus.Sold)
            throw new InvalidOperationException($"Only sold liens can be activated. Current status: '{Status}'.");

        Status          = LienStatus.Active;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Settle(decimal payoffAmount, Guid updatedByUserId)
    {
        if (Status != LienStatus.Active && Status != LienStatus.Disputed)
            throw new InvalidOperationException($"Only active or disputed liens can be settled. Current status: '{Status}'.");

        if (payoffAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(payoffAmount), "Payoff amount cannot be negative.");

        PayoffAmount    = payoffAmount;
        CurrentBalance  = 0;
        Status          = LienStatus.Settled;
        ClosedAtUtc     = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void UpdateSellingAnalyticsFields(
        Guid updatedByUserId,
        string? sellerStatus = null,
        string? listingVisibility = null,
        Guid? fundingCompanyId = null,
        Guid? fundingCompanyContactId = null,
        decimal? askAmount = null,
        decimal? highestBidAmount = null,
        DateTime? submittedForSaleAtUtc = null,
        DateTime? soldAtUtc = null,
        DateTime? withdrawnAtUtc = null,
        DateTime? archivedAtUtc = null,
        string? archivedReason = null)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        if (!string.IsNullOrWhiteSpace(sellerStatus) && !SellingLienStatus.All.Contains(sellerStatus))
            throw new ArgumentException($"Invalid seller status: '{sellerStatus}'.", nameof(sellerStatus));

        if (!string.IsNullOrWhiteSpace(listingVisibility) && !SellingListingVisibility.All.Contains(listingVisibility))
            throw new ArgumentException($"Invalid listing visibility: '{listingVisibility}'.", nameof(listingVisibility));

        if (askAmount.HasValue && askAmount.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(askAmount), "Ask amount cannot be negative.");

        if (highestBidAmount.HasValue && highestBidAmount.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(highestBidAmount), "Highest bid amount cannot be negative.");

        SellerStatus = string.IsNullOrWhiteSpace(sellerStatus) ? SellerStatus : sellerStatus;
        ListingVisibility = string.IsNullOrWhiteSpace(listingVisibility) ? ListingVisibility : listingVisibility;
        FundingCompanyId = fundingCompanyId ?? FundingCompanyId;
        FundingCompanyContactId = fundingCompanyContactId ?? FundingCompanyContactId;
        AskAmount = askAmount ?? AskAmount;
        HighestBidAmount = highestBidAmount ?? HighestBidAmount;
        SubmittedForSaleAtUtc = submittedForSaleAtUtc ?? SubmittedForSaleAtUtc;
        SoldAtUtc = soldAtUtc ?? SoldAtUtc;
        WithdrawnAtUtc = withdrawnAtUtc ?? WithdrawnAtUtc;
        ArchivedAtUtc = archivedAtUtc ?? ArchivedAtUtc;
        ArchivedReason = archivedReason?.Trim() ?? ArchivedReason;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RestoreFromArchive(Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        SellerStatus = SellingLienStatus.Pending;
        ArchivedAtUtc = null;
        ArchivedReason = null;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetBuyerMessage(string? buyerMessage, Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        var normalized = buyerMessage?.Trim();
        if (normalized?.Length > 4000)
            throw new ArgumentOutOfRangeException(nameof(buyerMessage), "Buyer message cannot exceed 4000 characters.");

        BuyerMessage = normalized;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetFinancials(
        decimal originalAmount,
        Guid updatedByUserId,
        decimal? currentBalance = null,
        decimal? offerPrice = null,
        decimal? purchasePrice = null,
        decimal? payoffAmount = null)
    {
        if (originalAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "Original amount cannot be negative.");
        if (currentBalance.HasValue && currentBalance.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(currentBalance), "Current balance cannot be negative.");
        if (offerPrice.HasValue && offerPrice.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(offerPrice), "Offer price cannot be negative.");
        if (purchasePrice.HasValue && purchasePrice.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(purchasePrice), "Purchase price cannot be negative.");
        if (payoffAmount.HasValue && payoffAmount.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(payoffAmount), "Payoff amount cannot be negative.");

        OriginalAmount = originalAmount;
        if (currentBalance.HasValue) CurrentBalance = currentBalance.Value;
        if (offerPrice.HasValue)     OfferPrice     = offerPrice.Value;
        if (purchasePrice.HasValue)  PurchasePrice  = purchasePrice.Value;
        if (payoffAmount.HasValue)   PayoffAmount   = payoffAmount.Value;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void AttachCase(Guid caseId, Guid updatedByUserId)
    {
        if (caseId == Guid.Empty) throw new ArgumentException("CaseId is required.", nameof(caseId));

        CaseId          = caseId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void DetachCase(Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        CaseId          = null;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void DetachSellingCase(Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        SellingCaseId   = null;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void AttachFacility(Guid facilityId, Guid updatedByUserId)
    {
        if (facilityId == Guid.Empty) throw new ArgumentException("FacilityId is required.", nameof(facilityId));

        FacilityId      = facilityId;
        MedicalFacilityCompanyId = null;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void TransferHolding(Guid newHoldingOrgId, Guid updatedByUserId)
    {
        if (newHoldingOrgId == Guid.Empty) throw new ArgumentException("NewHoldingOrgId is required.", nameof(newHoldingOrgId));

        HoldingOrgId    = newHoldingOrgId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
