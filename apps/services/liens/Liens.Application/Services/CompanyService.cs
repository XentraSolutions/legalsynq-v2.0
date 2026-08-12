using System.Net.Mail;
using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;

namespace Liens.Application.Services;

public sealed class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;

    public CompanyService(ICompanyRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<CompanyTypeResponse>> GetCompanyTypesAsync(CancellationToken ct = default)
        => (await _repository.GetCompanyTypesAsync(ct)).Select(Map).ToList();

    public async Task<List<ContactPersonTypeResponse>> GetContactPersonTypesAsync(
        Guid companyTypeId, CancellationToken ct = default)
    {
        var companyType = await _repository.GetCompanyTypeAsync(companyTypeId, ct);
        if (companyType is null || !companyType.IsActive)
            throw new NotFoundException($"Company type '{companyTypeId}' not found.");

        return (await _repository.GetContactPersonTypesAsync(companyTypeId, ct)).Select(Map).ToList();
    }

    public async Task<PaginatedResult<CompanyResponse>> SearchCompaniesAsync(
        Guid tenantId, Guid orgId, string? search, Guid? companyTypeId, bool? isActive,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var (items, totalCount) = await _repository.SearchCompaniesAsync(
            tenantId, orgId, search, companyTypeId, isActive, page, pageSize, ct);
        return new PaginatedResult<CompanyResponse>
        {
            Items = items.Select(Map).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<CompanyResponse?> GetCompanyAsync(
        Guid tenantId, Guid orgId, Guid id, CancellationToken ct = default)
    {
        var company = await _repository.GetCompanyAsync(tenantId, orgId, id, ct);
        return company is null ? null : Map(company);
    }

    public async Task<CompanyResponse> CreateCompanyAsync(
        Guid tenantId, Guid orgId, Guid actingUserId, CreateCompanyRequest request, CancellationToken ct = default)
    {
        ValidateCompany(request.Name, request.LinkedTenantId, request.AddressLine1, request.City,
            request.State, request.PostalCode, request.Phone, request.Email);
        var type = await _repository.GetCompanyTypeAsync(request.CompanyTypeId, ct);
        if (type is null || !type.IsActive)
            throw Validation("companyTypeId", "Company type is invalid or inactive.");

        var normalizedName = Company.NormalizeName(request.Name);
        if (await _repository.CompanyNameExistsAsync(
                tenantId, orgId, request.CompanyTypeId, normalizedName, ct: ct))
            throw new ConflictException("A company with this name and type already exists.");

        var company = Company.Create(
            tenantId, orgId, request.CompanyTypeId, request.Name, actingUserId,
            request.LinkedTenantId, request.AddressLine1, request.City, request.State,
            request.PostalCode, request.Phone, request.Email);
        await _repository.AddCompanyAsync(company, ct);
        company = await _repository.GetCompanyAsync(tenantId, orgId, company.Id, ct) ?? company;
        return Map(company);
    }

    public async Task<CompanyResponse> UpdateCompanyAsync(
        Guid tenantId, Guid orgId, Guid id, Guid actingUserId,
        UpdateCompanyRequest request, CancellationToken ct = default)
    {
        ValidateCompany(request.Name, request.LinkedTenantId, request.AddressLine1, request.City,
            request.State, request.PostalCode, request.Phone, request.Email);
        var company = await RequireCompanyAsync(tenantId, orgId, id, ct);
        if (!company.IsActive)
            throw new ConflictException("Inactive companies cannot be updated.");

        var normalizedName = Company.NormalizeName(request.Name);
        if (await _repository.CompanyNameExistsAsync(
                tenantId, orgId, company.CompanyTypeId, normalizedName, company.Id, ct))
            throw new ConflictException("A company with this name and type already exists.");

        company.Update(request.Name, actingUserId, request.LinkedTenantId, request.AddressLine1,
            request.City, request.State, request.PostalCode, request.Phone, request.Email);
        await _repository.UpdateCompanyAsync(company, ct);
        return Map(company);
    }

    public async Task<CompanyResponse> SetCompanyActiveAsync(
        Guid tenantId, Guid orgId, Guid id, Guid actingUserId,
        bool isActive, CancellationToken ct = default)
    {
        var company = await RequireCompanyAsync(tenantId, orgId, id, ct);
        if (isActive) company.Reactivate(actingUserId); else company.Deactivate(actingUserId);
        await _repository.UpdateCompanyAsync(company, ct);
        return Map(company);
    }

    public async Task<List<CompanyContactPersonResponse>> GetContactPersonsAsync(
        Guid tenantId, Guid orgId, Guid companyId, bool? isActive, CancellationToken ct = default)
    {
        await RequireCompanyAsync(tenantId, orgId, companyId, ct);
        return (await _repository.GetContactPersonsAsync(tenantId, companyId, isActive, ct)).Select(Map).ToList();
    }

    public async Task<CompanyContactPersonResponse?> GetContactPersonAsync(
        Guid tenantId, Guid orgId, Guid companyId, Guid contactId, CancellationToken ct = default)
    {
        await RequireCompanyAsync(tenantId, orgId, companyId, ct);
        var contact = await _repository.GetContactPersonAsync(tenantId, companyId, contactId, ct);
        return contact is null ? null : Map(contact);
    }

    public async Task<CompanyContactPersonResponse> CreateContactPersonAsync(
        Guid tenantId, Guid orgId, Guid companyId, Guid actingUserId,
        CreateCompanyContactPersonRequest request, CancellationToken ct = default)
    {
        var company = await RequireCompanyAsync(tenantId, orgId, companyId, ct);
        if (!company.IsActive)
            throw new ConflictException("Contacts cannot be added to an inactive company.");
        ValidateContact(request.ContactPersonTypeId, request.FirstName, request.LastName,
            request.AddressLine1, request.City, request.State, request.PostalCode, request.Phone, request.Email);
        await RequireMatchingRoleAsync(company, request.ContactPersonTypeId, ct);

        var contact = CompanyContactPerson.Create(
            tenantId, companyId, request.ContactPersonTypeId, request.FirstName, request.LastName,
            actingUserId, request.AddressLine1, request.City, request.State, request.PostalCode,
            request.Phone, request.Email);
        await _repository.AddContactPersonAsync(contact, ct);
        contact = await _repository.GetContactPersonAsync(tenantId, companyId, contact.Id, ct) ?? contact;
        return Map(contact);
    }

    public async Task<CompanyContactPersonResponse> UpdateContactPersonAsync(
        Guid tenantId, Guid orgId, Guid companyId, Guid contactId, Guid actingUserId,
        UpdateCompanyContactPersonRequest request, CancellationToken ct = default)
    {
        var company = await RequireCompanyAsync(tenantId, orgId, companyId, ct);
        if (!company.IsActive)
            throw new ConflictException("Contacts for an inactive company cannot be updated.");
        ValidateContact(request.ContactPersonTypeId, request.FirstName, request.LastName,
            request.AddressLine1, request.City, request.State, request.PostalCode, request.Phone, request.Email);
        await RequireMatchingRoleAsync(company, request.ContactPersonTypeId, ct);
        var contact = await RequireContactAsync(tenantId, companyId, contactId, ct);
        if (!contact.IsActive)
            throw new ConflictException("Inactive contacts cannot be updated.");

        contact.Update(request.ContactPersonTypeId, request.FirstName, request.LastName, actingUserId,
            request.AddressLine1, request.City, request.State, request.PostalCode, request.Phone, request.Email);
        await _repository.UpdateContactPersonAsync(contact, ct);
        return Map(contact);
    }

    public async Task<CompanyContactPersonResponse> SetContactPersonActiveAsync(
        Guid tenantId, Guid orgId, Guid companyId, Guid contactId, Guid actingUserId,
        bool isActive, CancellationToken ct = default)
    {
        var company = await RequireCompanyAsync(tenantId, orgId, companyId, ct);
        if (isActive && !company.IsActive)
            throw new ConflictException("Contacts cannot be reactivated while the company is inactive.");
        var contact = await RequireContactAsync(tenantId, companyId, contactId, ct);
        if (isActive) contact.Reactivate(actingUserId); else contact.Deactivate(actingUserId);
        await _repository.UpdateContactPersonAsync(contact, ct);
        return Map(contact);
    }

    private async Task<Company> RequireCompanyAsync(Guid tenantId, Guid orgId, Guid id, CancellationToken ct)
        => await _repository.GetCompanyAsync(tenantId, orgId, id, ct)
           ?? throw new NotFoundException($"Company '{id}' not found.");

    private async Task<CompanyContactPerson> RequireContactAsync(
        Guid tenantId, Guid companyId, Guid contactId, CancellationToken ct)
        => await _repository.GetContactPersonAsync(tenantId, companyId, contactId, ct)
           ?? throw new NotFoundException($"Company contact '{contactId}' not found.");

    private async Task RequireMatchingRoleAsync(Company company, Guid roleId, CancellationToken ct)
    {
        var role = await _repository.GetContactPersonTypeAsync(roleId, ct);
        if (role is null || !role.IsActive || role.CompanyTypeId != company.CompanyTypeId)
            throw Validation("contactPersonTypeId", "Contact-person type is invalid, inactive, or does not belong to the company's type.");
    }

    private static void ValidateCompany(
        string name, Guid? linkedTenantId, string? address, string? city,
        string? state, string? postalCode, string? phone, string? email)
    {
        var errors = new Dictionary<string, string[]>();
        Required(errors, "name", name, 200);
        Optional(errors, "addressLine1", address, 300);
        Optional(errors, "city", city, 100);
        Optional(errors, "state", state, 100);
        Optional(errors, "postalCode", postalCode, 20);
        Optional(errors, "phone", phone, 30);
        ValidateEmail(errors, email);
        if (linkedTenantId == Guid.Empty) errors["linkedTenantId"] = ["Linked tenant ID cannot be empty."];
        ThrowIfErrors(errors);
    }

    private static void ValidateContact(
        Guid roleId, string firstName, string lastName, string? address, string? city,
        string? state, string? postalCode, string? phone, string? email)
    {
        var errors = new Dictionary<string, string[]>();
        if (roleId == Guid.Empty) errors["contactPersonTypeId"] = ["Contact-person type is required."];
        Required(errors, "firstName", firstName, 100);
        Required(errors, "lastName", lastName, 100);
        Optional(errors, "addressLine1", address, 300);
        Optional(errors, "city", city, 100);
        Optional(errors, "state", state, 100);
        Optional(errors, "postalCode", postalCode, 20);
        Optional(errors, "phone", phone, 30);
        ValidateEmail(errors, email);
        ThrowIfErrors(errors);
    }

    private static void Required(Dictionary<string, string[]> errors, string key, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) errors[key] = [$"{key} is required."];
        else Optional(errors, key, value, maxLength);
    }

    private static void Optional(Dictionary<string, string[]> errors, string key, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength) errors[key] = [$"{key} must not exceed {maxLength} characters."];
    }

    private static void ValidateEmail(Dictionary<string, string[]> errors, string? email)
    {
        Optional(errors, "email", email, 320);
        if (string.IsNullOrWhiteSpace(email) || errors.ContainsKey("email")) return;
        try
        {
            var parsed = new MailAddress(email.Trim());
            if (!string.Equals(parsed.Address, email.Trim(), StringComparison.OrdinalIgnoreCase))
                errors["email"] = ["Email is invalid."];
        }
        catch (FormatException)
        {
            errors["email"] = ["Email is invalid."];
        }
    }

    private static void ThrowIfErrors(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0) throw new ValidationException("Company data is invalid.", errors);
    }

    private static ValidationException Validation(string key, string message)
        => new("Company data is invalid.", new Dictionary<string, string[]> { [key] = [message] });

    private static CompanyTypeResponse Map(CompanyType value) => new()
    {
        Id = value.Id, Code = value.Code, Name = value.Name, SortOrder = value.SortOrder,
    };

    private static ContactPersonTypeResponse Map(ContactPersonType value) => new()
    {
        Id = value.Id, CompanyTypeId = value.CompanyTypeId, Code = value.Code,
        Name = value.Name, SortOrder = value.SortOrder,
    };

    private static CompanyResponse Map(Company value) => new()
    {
        Id = value.Id,
        CompanyTypeId = value.CompanyTypeId,
        CompanyTypeCode = value.CompanyType?.Code ?? string.Empty,
        CompanyTypeName = value.CompanyType?.Name ?? string.Empty,
        LinkedTenantId = value.LinkedTenantId,
        Name = value.Name,
        AddressLine1 = value.AddressLine1,
        City = value.City,
        State = value.State,
        PostalCode = value.PostalCode,
        Phone = value.Phone,
        Email = value.Email,
        IsActive = value.IsActive,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static CompanyContactPersonResponse Map(CompanyContactPerson value) => new()
    {
        Id = value.Id,
        CompanyId = value.CompanyId,
        ContactPersonTypeId = value.ContactPersonTypeId,
        ContactPersonTypeCode = value.ContactPersonType?.Code ?? string.Empty,
        ContactPersonTypeName = value.ContactPersonType?.Name ?? string.Empty,
        FirstName = value.FirstName,
        LastName = value.LastName,
        AddressLine1 = value.AddressLine1,
        City = value.City,
        State = value.State,
        PostalCode = value.PostalCode,
        Phone = value.Phone,
        Email = value.Email,
        IsActive = value.IsActive,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };
}
