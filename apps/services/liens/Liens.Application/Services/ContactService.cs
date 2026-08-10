using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class ContactService : IContactService
{
    private readonly IContactRepository _repo;
    private readonly IFacilityRepository _facilityRepo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        IContactRepository repo,
        IFacilityRepository facilityRepo,
        IAuditPublisher audit,
        ILogger<ContactService> logger)
    {
        _repo = repo;
        _facilityRepo = facilityRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PaginatedResult<ContactResponse>> SearchAsync(
        Guid tenantId, string? search, string? contactType, bool? isActive,
        int page, int pageSize, Guid? lawFirmId = null, Guid? facilityId = null, string? contactSubtype = null, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        if (facilityId.HasValue && IsParentFacilitySearch(contactType, contactSubtype))
        {
            var parentFacilityContact = await _repo.GetFacilityContactByReferenceAsync(tenantId, facilityId.Value, ct);
            var parentMatches = parentFacilityContact is not null &&
                string.Equals(parentFacilityContact.ContactType, contactType, StringComparison.Ordinal) &&
                IsStandaloneFacilityContact(parentFacilityContact) &&
                (!isActive.HasValue || parentFacilityContact.IsActive == isActive.Value) &&
                MatchesSearch(parentFacilityContact, search);
            var parentActiveCaseCounts = parentFacilityContact is null
                ? new Dictionary<Guid, int>()
                : await _repo.GetActiveCaseCountsAsync(tenantId, new[] { parentFacilityContact }, ct);

            var parentItems = parentMatches
                ? new List<ContactResponse>
                {
                    MapToResponse(
                        parentFacilityContact!,
                        parentActiveCaseCounts.GetValueOrDefault(parentFacilityContact!.Id))
                }
                : new List<ContactResponse>();

            return new PaginatedResult<ContactResponse>
            {
                Items = parentItems.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = parentItems.Count,
            };
        }

        var resolvedFacilityId = facilityId.HasValue
            ? await ResolveFacilityFilterIdAsync(tenantId, facilityId.Value, ct)
            : (Guid?)null;

        var (items, totalCount) = await _repo.SearchAsync(
            tenantId, search, contactType, isActive, page, pageSize, lawFirmId, resolvedFacilityId, contactSubtype, ct);
        var activeCaseCounts = await _repo.GetActiveCaseCountsAsync(tenantId, items, ct);

        return new PaginatedResult<ContactResponse>
        {
            Items = items.Select(item => MapToResponse(item, activeCaseCounts.GetValueOrDefault(item.Id))).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<ContactResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct);
        if (entity is null)
            return null;

        var activeCaseCounts = await _repo.GetActiveCaseCountsAsync(tenantId, new[] { entity }, ct);
        return MapToResponse(entity, activeCaseCounts.GetValueOrDefault(entity.Id));
    }

    public async Task<ContactResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateContactRequest request, CancellationToken ct = default)
    {
        var (firstName, lastName) = ResolveContactNames(
            request.FullName,
            request.FirstName,
            request.LastName);
        var resolvedFacilityId = request.FacilityId.HasValue
            ? await ResolveFacilityIdAsync(tenantId, request.FacilityId.Value, actingUserId, ct)
            : (Guid?)null;

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(firstName))
            errors["firstName"] = new[] { "First name is required." };
        if (RequiresLastName(request.ContactType, request.ContactSubtype, request.LawFirmId) &&
            string.IsNullOrWhiteSpace(lastName))
            errors["lastName"] = new[] { "Last name is required." };
        if (!ContactType.All.Contains(request.ContactType))
            errors["contactType"] = new[] { $"Invalid contact type: '{request.ContactType}'. Valid types: {string.Join(", ", ContactType.All)}" };
        if (!string.IsNullOrWhiteSpace(request.ContactSubtype) && !ContactSubtype.All.Contains(request.ContactSubtype))
            errors["contactSubtype"] = new[] { $"Invalid contact subtype: '{request.ContactSubtype}'." };
        if (request.FacilityId.HasValue && request.FacilityId.Value == Guid.Empty)
            errors["facilityId"] = new[] { "FacilityId cannot be empty." };
        if (request.LawFirmId.HasValue && request.LawFirmId.Value == Guid.Empty)
            errors["lawFirmId"] = new[] { "LawFirmId cannot be empty." };
        if (!string.IsNullOrWhiteSpace(request.ContactSubtype) && request.ContactSubtype == ContactSubtype.FacilityContactPerson)
        {
            if (request.ContactType != ContactType.MedicalFacility && request.ContactType != ContactType.Facility)
                errors["contactType"] = new[] { "Facility contact person subtype requires contactType 'Facility' or 'MedicalFacility'." };
            if (!request.FacilityId.HasValue)
                errors["facilityId"] = new[] { "FacilityId is required for facility contact person subtype." };
        }
        if (!string.IsNullOrWhiteSpace(request.ContactSubtype) && ContactSubtype.LawFirmRoles.Contains(request.ContactSubtype))
        {
            if (request.ContactType != ContactType.LawFirm)
                errors["contactType"] = new[] { "Law firm contact subtypes require contactType 'LawFirm'." };
            if (!request.LawFirmId.HasValue)
                errors["lawFirmId"] = new[] { "LawFirmId is required for law firm contact subtypes." };
        }
        if (request.LawFirmId.HasValue && string.IsNullOrWhiteSpace(request.ContactSubtype))
        {
            errors["lawFirmId"] = new[] { "LawFirmId can only be used with law firm contact subtypes." };
        }
        if (request.FacilityId.HasValue && !resolvedFacilityId.HasValue)
        {
            errors["facilityId"] = new[] { $"Facility '{request.FacilityId.Value}' not found." };
        }
        Contact? parentLawFirm = null;
        if (request.LawFirmId.HasValue)
        {
            parentLawFirm = await _repo.GetByIdAsync(tenantId, request.LawFirmId.Value, ct);
            if (parentLawFirm is null)
            {
                errors["lawFirmId"] = new[] { $"Law firm '{request.LawFirmId.Value}' not found." };
            }
            else if (parentLawFirm.ContactType != ContactType.LawFirm || !string.IsNullOrWhiteSpace(parentLawFirm.ContactSubtype))
            {
                errors["lawFirmId"] = new[] { $"Contact '{request.LawFirmId.Value}' is not a parent law firm." };
            }
        }
        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);

        try
        {
            var entity = Contact.Create(
                tenantId, orgId, request.ContactType,
                firstName, lastName, actingUserId,
                resolvedFacilityId, request.LawFirmId, request.ContactSubtype,
                request.Title, ResolveOrganization(
                    request.Organization,
                    parentLawFirm,
                    request.ContactType,
                    request.ContactSubtype,
                    request.LawFirmId,
                    firstName,
                    lastName),
                request.Email, request.Phone, request.Fax, request.Website,
                request.AddressLine1, request.City, request.State, request.PostalCode,
                request.Notes);

            await _repo.AddAsync(entity, ct);

            _logger.LogInformation(
                "Contact created: {ContactId} {DisplayName} Type={Type} Tenant={TenantId}",
                entity.Id, entity.DisplayName, entity.ContactType, tenantId);

            _audit.Publish(
                eventType: "liens.contact.created",
                action: "create",
                description: $"Contact '{entity.DisplayName}' created",
                tenantId: tenantId,
                actorUserId: actingUserId,
                entityType: "Contact",
                entityId: entity.Id.ToString());

            return MapToResponse(entity);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message,
                new Dictionary<string, string[]> { [ex.ParamName ?? "unknown"] = new[] { ex.Message } });
        }
    }

    public async Task<ContactResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateContactRequest request, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Contact '{id}' not found for tenant '{tenantId}'.");
        var (firstName, lastName) = ResolveContactNames(
            request.FullName,
            request.FirstName,
            request.LastName);
        var resolvedFacilityId = request.FacilityId.HasValue
            ? await ResolveFacilityIdAsync(tenantId, request.FacilityId.Value, actingUserId, ct)
            : (Guid?)null;

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(firstName))
            errors["firstName"] = new[] { "First name is required." };
        if (RequiresLastName(request.ContactType, request.ContactSubtype, request.LawFirmId) &&
            string.IsNullOrWhiteSpace(lastName))
            errors["lastName"] = new[] { "Last name is required." };
        if (!ContactType.All.Contains(request.ContactType))
            errors["contactType"] = new[] { $"Invalid contact type: '{request.ContactType}'." };
        if (!string.IsNullOrWhiteSpace(request.ContactSubtype) && !ContactSubtype.All.Contains(request.ContactSubtype))
            errors["contactSubtype"] = new[] { $"Invalid contact subtype: '{request.ContactSubtype}'." };
        if (request.FacilityId.HasValue && request.FacilityId.Value == Guid.Empty)
            errors["facilityId"] = new[] { "FacilityId cannot be empty." };
        if (request.LawFirmId.HasValue && request.LawFirmId.Value == Guid.Empty)
            errors["lawFirmId"] = new[] { "LawFirmId cannot be empty." };
        if (!string.IsNullOrWhiteSpace(request.ContactSubtype) && request.ContactSubtype == ContactSubtype.FacilityContactPerson)
        {
            if (request.ContactType != ContactType.MedicalFacility && request.ContactType != ContactType.Facility)
                errors["contactType"] = new[] { "Facility contact person subtype requires contactType 'Facility' or 'MedicalFacility'." };
            if (!request.FacilityId.HasValue)
                errors["facilityId"] = new[] { "FacilityId is required for facility contact person subtype." };
        }
        if (!string.IsNullOrWhiteSpace(request.ContactSubtype) && ContactSubtype.LawFirmRoles.Contains(request.ContactSubtype))
        {
            if (request.ContactType != ContactType.LawFirm)
                errors["contactType"] = new[] { "Law firm contact subtypes require contactType 'LawFirm'." };
            if (!request.LawFirmId.HasValue)
                errors["lawFirmId"] = new[] { "LawFirmId is required for law firm contact subtypes." };
        }
        if (request.LawFirmId.HasValue && string.IsNullOrWhiteSpace(request.ContactSubtype))
        {
            errors["lawFirmId"] = new[] { "LawFirmId can only be used with law firm contact subtypes." };
        }
        if (request.FacilityId.HasValue && !resolvedFacilityId.HasValue)
        {
            errors["facilityId"] = new[] { $"Facility '{request.FacilityId.Value}' not found." };
        }
        Contact? parentLawFirm = null;
        if (request.LawFirmId.HasValue)
        {
            if (request.LawFirmId.Value == id)
            {
                errors["lawFirmId"] = new[] { "LawFirmId cannot reference the same contact." };
            }
            else
            {
                parentLawFirm = await _repo.GetByIdAsync(tenantId, request.LawFirmId.Value, ct);
                if (parentLawFirm is null)
                {
                    errors["lawFirmId"] = new[] { $"Law firm '{request.LawFirmId.Value}' not found." };
                }
                else if (parentLawFirm.ContactType != ContactType.LawFirm || !string.IsNullOrWhiteSpace(parentLawFirm.ContactSubtype))
                {
                    errors["lawFirmId"] = new[] { $"Contact '{request.LawFirmId.Value}' is not a parent law firm." };
                }
            }
        }
        if (errors.Count > 0)
            throw new ValidationException("One or more fields are invalid.", errors);

        try
        {
            entity.Update(
                firstName, lastName, request.ContactType, actingUserId,
                resolvedFacilityId, request.LawFirmId, request.ContactSubtype,
                request.Title, ResolveOrganization(
                    request.Organization,
                    parentLawFirm,
                    request.ContactType,
                    request.ContactSubtype,
                    request.LawFirmId,
                    firstName,
                    lastName,
                    IsStandaloneLawFirm(entity.ContactType, entity.ContactSubtype, entity.LawFirmId)
                        ? entity.Organization
                        : null,
                    IsStandaloneLawFirm(entity.ContactType, entity.ContactSubtype, entity.LawFirmId)
                        ? entity.DisplayName
                        : null),
                request.Email, request.Phone, request.Fax, request.Website,
                request.AddressLine1, request.City, request.State, request.PostalCode,
                request.Notes);

            await _repo.UpdateAsync(entity, ct);

            _logger.LogInformation(
                "Contact updated: {ContactId} Tenant={TenantId}", entity.Id, tenantId);

            _audit.Publish(
                eventType: "liens.contact.updated",
                action: "update",
                description: $"Contact '{entity.DisplayName}' updated",
                tenantId: tenantId,
                actorUserId: actingUserId,
                entityType: "Contact",
                entityId: entity.Id.ToString());

            return MapToResponse(entity);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message,
                new Dictionary<string, string[]> { [ex.ParamName ?? "unknown"] = new[] { ex.Message } });
        }
    }

    public async Task<ContactResponse> DeactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Contact '{id}' not found for tenant '{tenantId}'.");

        entity.Deactivate(actingUserId);
        await _repo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Contact deactivated: {ContactId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType: "liens.contact.deactivated",
            action: "update",
            description: $"Contact '{entity.DisplayName}' deactivated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Contact",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<ContactResponse> ReactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Contact '{id}' not found for tenant '{tenantId}'.");

        entity.Reactivate(actingUserId);
        await _repo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Contact reactivated: {ContactId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType: "liens.contact.reactivated",
            action: "update",
            description: $"Contact '{entity.DisplayName}' reactivated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Contact",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<List<ContactResponse>> GetAllByTypeAsync(
        Guid tenantId, string? contactType, bool? isActive = true, CancellationToken ct = default)
    {
        var items = await _repo.GetAllByTypeAsync(tenantId, contactType, isActive, ct);
        var activeCaseCounts = await _repo.GetActiveCaseCountsAsync(tenantId, items, ct);
        return items.Select(item => MapToResponse(item, activeCaseCounts.GetValueOrDefault(item.Id))).ToList();
    }

    public async Task<IReadOnlyList<Guid>> FindLawFirmFilterIdsAsync(
        Guid tenantId,
        string lawFirmName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(lawFirmName))
            return [];

        var term = lawFirmName.Trim();
        var matchingLawFirms = (await _repo.GetAllByTypeAsync(
                tenantId,
                ContactType.LawFirm,
                isActive: null,
                ct))
            .Where(contact =>
            {
                var firmName = string.IsNullOrWhiteSpace(contact.Organization)
                    ? contact.DisplayName
                    : contact.Organization;
                return firmName.Contains(term, StringComparison.OrdinalIgnoreCase);
            });

        return matchingLawFirms
            .SelectMany(contact => new Guid?[] { contact.Id, contact.OrgId, contact.LawFirmId })
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private static ContactResponse MapToResponse(Contact entity, int activeCases = 0) => new()
    {
        Id = entity.Id,
        FacilityId = entity.FacilityId,
        LawFirmId = entity.LawFirmId,
        ContactType = entity.ContactType,
        ContactSubtype = entity.ContactSubtype,
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        DisplayName = entity.DisplayName,
        Title = entity.Title,
        Organization = entity.Organization,
        Email = entity.Email,
        Phone = entity.Phone,
        Fax = entity.Fax,
        Website = entity.Website,
        AddressLine1 = entity.AddressLine1,
        City = entity.City,
        State = entity.State,
        PostalCode = entity.PostalCode,
        Notes = entity.Notes,
        IsActive = entity.IsActive,
        ActiveCases = activeCases,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
    };

    private static string? ResolveOrganization(
        string? organization,
        Contact? parentLawFirm,
        string contactType,
        string? contactSubtype,
        Guid? lawFirmId,
        string firstName,
        string lastName,
        string? existingStandaloneLawFirmOrganization = null,
        string? existingStandaloneLawFirmDisplayName = null)
    {
        if (!string.IsNullOrWhiteSpace(organization))
            return organization;

        if (parentLawFirm is not null)
            return parentLawFirm.Organization ?? parentLawFirm.DisplayName;

        if (!IsStandaloneLawFirm(contactType, contactSubtype, lawFirmId))
            return null;

        if (!string.IsNullOrWhiteSpace(existingStandaloneLawFirmOrganization) &&
            !string.Equals(
                existingStandaloneLawFirmOrganization.Trim(),
                existingStandaloneLawFirmDisplayName?.Trim(),
                StringComparison.Ordinal))
            return existingStandaloneLawFirmOrganization;

        return string.Join(' ', new[] { firstName.Trim(), lastName.Trim() }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static (string FirstName, string LastName) ResolveContactNames(
        string? fullName,
        string? firstName,
        string? lastName)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
            return SplitFullName(fullName);

        return (firstName?.Trim() ?? string.Empty, lastName?.Trim() ?? string.Empty);
    }

    private static (string FirstName, string LastName) SplitFullName(string fullName)
    {
        var parts = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return (string.Empty, string.Empty);

        if (parts.Length == 1)
            return (parts[0], string.Empty);

        return (string.Join(" ", parts[..^1]), parts[^1]);
    }

    private static bool RequiresLastName(string contactType, string? contactSubtype, Guid? lawFirmId)
        => !IsStandaloneLawFirm(contactType, contactSubtype, lawFirmId);

    private static bool IsStandaloneLawFirm(string contactType, string? contactSubtype, Guid? lawFirmId)
        => string.Equals(contactType, ContactType.LawFirm, StringComparison.Ordinal)
           && string.IsNullOrWhiteSpace(contactSubtype)
           && !lawFirmId.HasValue;

    private async Task<Guid?> ResolveFacilityIdAsync(
        Guid tenantId,
        Guid requestedFacilityId,
        Guid actingUserId,
        CancellationToken ct)
    {
        if (requestedFacilityId == Guid.Empty)
            return null;

        var facility = await _facilityRepo.GetByIdAsync(tenantId, requestedFacilityId, ct);
        if (facility is not null)
            return facility.Id;

        var legacyFacilityContact = await _repo.GetByIdAsync(tenantId, requestedFacilityId, ct);
        if (legacyFacilityContact is null || !IsStandaloneFacilityContact(legacyFacilityContact))
            return null;

        if (legacyFacilityContact.FacilityId.HasValue)
        {
            var linkedFacility = await _facilityRepo.GetByIdAsync(tenantId, legacyFacilityContact.FacilityId.Value, ct);
            if (linkedFacility is not null)
                return linkedFacility.Id;
        }

        var createdFacility = Facility.Create(
            legacyFacilityContact.TenantId,
            legacyFacilityContact.OrgId,
            ResolveFacilityName(legacyFacilityContact),
            actingUserId,
            addressLine1: legacyFacilityContact.AddressLine1,
            city: legacyFacilityContact.City,
            state: legacyFacilityContact.State,
            postalCode: legacyFacilityContact.PostalCode,
            phone: legacyFacilityContact.Phone,
            email: legacyFacilityContact.Email,
            fax: legacyFacilityContact.Fax);

        await _facilityRepo.AddAsync(createdFacility, ct);

        legacyFacilityContact.Update(
            legacyFacilityContact.FirstName,
            legacyFacilityContact.LastName,
            legacyFacilityContact.ContactType,
            actingUserId,
            facilityId: createdFacility.Id,
            contactSubtype: legacyFacilityContact.ContactSubtype,
            title: legacyFacilityContact.Title,
            organization: legacyFacilityContact.Organization,
            email: legacyFacilityContact.Email,
            phone: legacyFacilityContact.Phone,
            fax: legacyFacilityContact.Fax,
            website: legacyFacilityContact.Website,
            addressLine1: legacyFacilityContact.AddressLine1,
            city: legacyFacilityContact.City,
            state: legacyFacilityContact.State,
            postalCode: legacyFacilityContact.PostalCode,
            notes: legacyFacilityContact.Notes);

        await _repo.UpdateAsync(legacyFacilityContact, ct);

        _logger.LogInformation(
            "Legacy facility contact {ContactId} linked to facility {FacilityId} for tenant {TenantId}",
            legacyFacilityContact.Id,
            createdFacility.Id,
            tenantId);

        return createdFacility.Id;
    }

    private async Task<Guid?> ResolveFacilityFilterIdAsync(
        Guid tenantId,
        Guid requestedFacilityId,
        CancellationToken ct)
    {
        if (requestedFacilityId == Guid.Empty)
            return null;

        var facility = await _facilityRepo.GetByIdAsync(tenantId, requestedFacilityId, ct);
        if (facility is not null)
            return facility.Id;

        var legacyFacilityContact = await _repo.GetByIdAsync(tenantId, requestedFacilityId, ct);
        if (legacyFacilityContact is null || !IsStandaloneFacilityContact(legacyFacilityContact))
            return requestedFacilityId;

        if (legacyFacilityContact.FacilityId.HasValue)
            return legacyFacilityContact.FacilityId.Value;

        var fallbackUserId = legacyFacilityContact.UpdatedByUserId
            ?? legacyFacilityContact.CreatedByUserId
            ?? Guid.Empty;

        if (fallbackUserId == Guid.Empty)
            return requestedFacilityId;

        return await ResolveFacilityIdAsync(tenantId, requestedFacilityId, fallbackUserId, ct)
            ?? requestedFacilityId;
    }

    private static bool IsStandaloneFacilityContact(Contact contact) =>
        (contact.ContactType == ContactType.Facility || contact.ContactType == ContactType.MedicalFacility)
        && string.IsNullOrWhiteSpace(contact.ContactSubtype);

    private static bool IsParentFacilitySearch(string? contactType, string? contactSubtype) =>
        string.IsNullOrWhiteSpace(contactSubtype) &&
        (string.Equals(contactType, ContactType.Facility, StringComparison.Ordinal) ||
         string.Equals(contactType, ContactType.MedicalFacility, StringComparison.Ordinal));

    private static bool MatchesSearch(Contact contact, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        var term = search.Trim();
        return contact.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               contact.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               contact.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(contact.Email) && contact.Email.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(contact.Organization) && contact.Organization.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveFacilityName(Contact contact)
    {
        if (!string.IsNullOrWhiteSpace(contact.Organization))
            return contact.Organization.Trim();

        return contact.DisplayName;
    }
}
