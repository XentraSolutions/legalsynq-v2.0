namespace Liens.Application.DTOs;

public sealed class CompanyTypeResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public sealed class ContactPersonTypeResponse
{
    public Guid Id { get; init; }
    public Guid CompanyTypeId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsSystem { get; init; }
}

public sealed class CreateContactPersonTypeRequest
{
    public Guid CompanyTypeId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int? SortOrder { get; init; }
}

public sealed class CreateCompanyRequest
{
    public Guid CompanyTypeId { get; init; }
    public Guid? LinkedTenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
}

public sealed class UpdateCompanyRequest
{
    public Guid? LinkedTenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
}

public sealed class ReassignCompanyRequest
{
    public Guid TargetCompanyId { get; init; }
}

public sealed class CompanyResponse
{
    public Guid Id { get; init; }
    public Guid CompanyTypeId { get; init; }
    public string CompanyTypeCode { get; init; } = string.Empty;
    public string CompanyTypeName { get; init; } = string.Empty;
    public Guid? LinkedTenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class CompanyDetailsResponse
{
    public CompanyResponse Company { get; init; } = new();
    public int TotalCases { get; init; }
    public int ActiveCases { get; init; }
    public decimal TotalBillingForActiveCases { get; init; }
    public CompanyRecentCasesResponse RecentCases { get; init; } = new();
}

public sealed class CompanyRecentCasesResponse
{
    public List<CompanyRecentCaseResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}

public sealed class CompanyRecentCaseResponse
{
    public Guid Id { get; init; }
    public string CaseNumber { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public decimal BillingAmount { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class CreateCompanyContactPersonRequest
{
    public Guid ContactPersonTypeId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
}

public sealed class UpdateCompanyContactPersonRequest
{
    public Guid ContactPersonTypeId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
}

public sealed class ReassignCompanyContactPersonRequest
{
    public Guid TargetContactPersonId { get; init; }
}

public sealed class CompanyContactPersonResponse
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public Guid ContactPersonTypeId { get; init; }
    public string ContactPersonTypeCode { get; init; } = string.Empty;
    public string ContactPersonTypeName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class CompanyContactPersonExportResponse
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public Guid CompanyTypeId { get; init; }
    public string CompanyTypeCode { get; init; } = string.Empty;
    public string CompanyTypeName { get; init; } = string.Empty;
    public Guid ContactPersonTypeId { get; init; }
    public string ContactPersonTypeCode { get; init; } = string.Empty;
    public string ContactPersonTypeName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class ContactPersonDirectoryResponse
{
    public List<CompanyContactPersonExportResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int Limit { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}

public sealed class CompanyReassignmentResponse
{
    public Guid SourceCompanyId { get; init; }
    public string SourceCompanyName { get; init; } = string.Empty;
    public Guid TargetCompanyId { get; init; }
    public string TargetCompanyName { get; init; } = string.Empty;
    public Guid CompanyTypeId { get; init; }
    public int ReassignedContactPersonCount { get; init; }
    public int ReassignedLienCount { get; init; }
    public int ReassignedCaseCount { get; init; }
    public int ReassignedOfferCount { get; init; }
    public int ReassignedBuyerAccessLinkCount { get; init; }
    public int ReassignedPortfolioBuyerCount { get; init; }
    public int TotalReassignedCount { get; init; }
}

public sealed class CompanyContactPersonReassignmentResponse
{
    public Guid SourceContactPersonId { get; init; }
    public string SourceContactPersonName { get; init; } = string.Empty;
    public Guid TargetContactPersonId { get; init; }
    public string TargetContactPersonName { get; init; } = string.Empty;
    public Guid SourceCompanyId { get; init; }
    public Guid TargetCompanyId { get; init; }
    public Guid CompanyTypeId { get; init; }
    public Guid ContactPersonTypeId { get; init; }
    public int ReassignedLienCount { get; init; }
    public int ReassignedCaseCount { get; init; }
    public int ReassignedBuyerAccessLinkCount { get; init; }
    public int TotalReassignedCount { get; init; }
}
