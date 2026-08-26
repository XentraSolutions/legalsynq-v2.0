namespace Liens.Application.DTOs;

public sealed class CreateSellingLienRequest
{
    public Guid CaseId { get; init; }
    public string SellerStatus { get; init; } = string.Empty;
    public string? Source { get; init; }
}

public sealed class CreateSellingCaseDraftRequest
{
    public string? AccidentTypeId { get; init; }
    public string? AccidentState { get; init; }
    public DateOnly? DateOfLoss { get; init; }
    public Guid? HandlingLawFirmId { get; init; }
    public Guid? CaseManagerId { get; init; }
    public string? CaseTrackingNotes { get; init; }
}

public sealed class FinalizeSellingCaseDraftPlaintiffRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public DateOnly? Birthdate { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Gender { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Zipcode { get; init; }
}

public sealed class SaveSellingLienInformationRequest
{
    public string SellerStatus { get; init; } = string.Empty;
    public DateOnly? InitialServiceDate { get; init; }
    public DateOnly? EndServiceDate { get; init; }
    public string? ListingVisibility { get; init; }
    public string? Notes { get; init; }
    public System.Text.Json.JsonElement ReceivableDueDate { get; init; }
}

public sealed class SaveSellingCaseInformationRequest
{
    public Guid? FundingCompanyId { get; init; }
    public Guid? FundingCompanyContactId { get; init; }
    public Guid? FacilityId { get; init; }
    public Guid? MedicalProviderId { get; init; }
}

public sealed class SaveSellingMedicalPricingRequest
{
    public decimal? AskAmount { get; init; }
    public decimal? BillingAmount { get; init; }
    public List<SellingMedicalPricingRowRequest> Rows { get; init; } = [];
}

public sealed class SellingMedicalPricingRowRequest
{
    public string MedicalCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateOnly? ServiceDate { get; init; }
    public decimal BillingAmount { get; init; }
    public decimal MedicareCost { get; init; }
    public decimal TargetSaleAmount { get; init; }
}

public sealed class SaveSellingDocumentsRequest
{
    public List<SellingDocumentReferenceRequest> Documents { get; init; } = [];
}

public sealed class SellingDocumentReferenceRequest
{
    public Guid DocumentId { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
}

public sealed class PrepareSellingLienRequest
{
    public Guid? BuyerFundingCompanyId { get; init; }
    public Guid? BuyerContactId { get; init; }
    public decimal? AskAmount { get; init; }
    public string? ListingVisibility { get; init; }
    public string? MessageToBuyer { get; init; }
}

public sealed class WithdrawSellingLienRequest
{
    public string? Reason { get; init; }
}

public sealed class MoveSellingLienToManagementRequest
{
    public string? Reason { get; init; }
}

public sealed class MoveSellingLienToManagementV2Request
{
    public string? Reason { get; init; }
    public MoveSellingLienToManagementCaseInfoRequest? CaseInfo { get; init; }
}

public sealed class MoveSellingLienToManagementCaseInfoRequest
{
    public string? ClientFirstName { get; init; }
    public string? ClientLastName { get; init; }
    public DateOnly? ClientDob { get; init; }
    public string? ClientAddress { get; init; }
    public string? ClientCity { get; init; }
    public string? ClientState { get; init; }
    public string? ClientZipCode { get; init; }
    public bool? IsServicing { get; init; }
    public string? StatusLabel { get; init; }
    public string? AccidentTypeId { get; init; }
    public string? StateOfIncident { get; init; }
    public DateOnly? DateOfIncident { get; init; }
    public string? LawFirmId { get; init; }
    public string? CaseManagerId { get; init; }
    public string? Notes { get; init; }
}

public sealed class ArchiveSellingLienRequest
{
    public string? Reason { get; init; }
}

public sealed class CreateSellingBuyerAccessLinkRequest
{
    public Guid BuyerFundingCompanyId { get; init; }
    public Guid BuyerContactId { get; init; }
    public int? ExpiresInHours { get; init; }
}

public sealed class SubmitSellingBuyerOfferRequest
{
    public decimal OfferAmount { get; init; }
    public string? Message { get; init; }
}

public sealed class DeclineSellingBuyerLienRequest
{
    public string? Reason { get; init; }
}

public sealed class ReassignContactCasesRequest
{
    public Guid TargetContactId { get; init; }
    public string RelationshipType { get; init; } = string.Empty;
    public string Scope { get; init; } = "Selected";
    public List<Guid> CaseIds { get; init; } = [];
}
