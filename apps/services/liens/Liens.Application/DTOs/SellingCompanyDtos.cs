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
