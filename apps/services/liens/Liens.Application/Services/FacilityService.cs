using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class FacilityService : IFacilityService
{
    private readonly IFacilityRepository _facilityRepo;
    private readonly IContactRepository _contactRepo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<FacilityService> _logger;

    public FacilityService(
        IFacilityRepository facilityRepo,
        IContactRepository contactRepo,
        IAuditPublisher audit,
        ILogger<FacilityService> logger)
    {
        _facilityRepo = facilityRepo;
        _contactRepo  = contactRepo;
        _audit        = audit;
        _logger       = logger;
    }

    public async Task<PaginatedResult<FacilityResponse>> SearchAsync(
        Guid tenantId, string? search, bool? isActive,
        int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var (items, totalCount) = await _contactRepo.SearchFacilityContactsAsync(
            tenantId, search, isActive, page, pageSize, ct);

        return new PaginatedResult<FacilityResponse>
        {
            Items      = items.Select(MapFacilityContactToResponse).ToList(),
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<PaginatedResult<FacilityResponse>> SearchLienFacilitiesAsync(
        Guid tenantId, string? search, bool? isActive,
        int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var (items, totalCount) = await _facilityRepo.SearchAsync(
            tenantId, search, isActive, page, pageSize, ct);

        return new PaginatedResult<FacilityResponse>
        {
            Items = items.Select(facility => new FacilityResponse
            {
                Id = facility.Id,
                Name = facility.Name,
                Code = facility.Code,
                ExternalReference = facility.ExternalReference,
                AddressLine1 = facility.AddressLine1,
                AddressLine2 = facility.AddressLine2,
                City = facility.City,
                State = facility.State,
                PostalCode = facility.PostalCode,
                Phone = facility.Phone,
                Email = facility.Email,
                Fax = facility.Fax,
                IsActive = facility.IsActive,
                CreatedAtUtc = facility.CreatedAtUtc,
                UpdatedAtUtc = facility.UpdatedAtUtc,
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<List<FacilityResponse>> GetAllAsync(
        Guid tenantId, bool? isActive = true, CancellationToken ct = default)
    {
        var (items, _) = await _contactRepo.SearchFacilityContactsAsync(
            tenantId, search: null, isActive, page: 1, pageSize: 10_000, ct);
        return items.Select(MapFacilityContactToResponse).ToList();
    }

    public async Task<FacilityResponse?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await ResolveFacilityContactAsync(tenantId, id, ct);
        return entity is null ? null : MapFacilityContactToResponse(entity);
    }

    public async Task<FacilityResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateFacilityRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Facility name is required.",
                new Dictionary<string, string[]> { ["name"] = ["Facility name is required."] });

        var (firstName, lastName) = SplitFacilityName(request.Name);
        var entity = Contact.Create(
            tenantId, orgId, ContactType.Facility,
            firstName, lastName, actingUserId,
            title: null,
            organization: request.Name.Trim(),
            email: request.Email,
            phone: request.Phone,
            fax: request.Fax,
            addressLine1: request.AddressLine1,
            city: request.City,
            state: request.State,
            postalCode: request.PostalCode);

        await _contactRepo.AddAsync(entity, ct);
        await EnsureBackingFacilityLinkAsync(entity, request.Code, request.ExternalReference, actingUserId, ct);

        _logger.LogInformation("Facility contact created: {ContactId} '{Name}' Tenant={TenantId}",
            entity.Id, request.Name.Trim(), tenantId);

        _audit.Publish(
            eventType: "liens.facility.created",
            action: "create",
            description: $"Facility '{ResolveFacilityName(entity)}' created",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Facility",
            entityId: entity.Id.ToString());

        return MapFacilityContactToResponse(entity);
    }

    public async Task<FacilityResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateFacilityRequest request, CancellationToken ct = default)
    {
        var entity = await ResolveFacilityContactAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Facility '{id}' not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Facility name is required.",
                new Dictionary<string, string[]> { ["name"] = ["Facility name is required."] });

        var (firstName, lastName) = SplitFacilityName(request.Name);
        entity.Update(
            firstName,
            lastName,
            entity.ContactType,
            actingUserId,
            facilityId: entity.FacilityId,
            contactSubtype: entity.ContactSubtype,
            title: entity.Title,
            organization: request.Name.Trim(),
            email: request.Email,
            phone: request.Phone,
            fax: request.Fax,
            website: entity.Website,
            addressLine1: request.AddressLine1,
            city: request.City,
            state: request.State,
            postalCode: request.PostalCode,
            notes: entity.Notes);

        await _contactRepo.UpdateAsync(entity, ct);
        await EnsureBackingFacilityLinkAsync(entity, request.Code, request.ExternalReference, actingUserId, ct);

        _logger.LogInformation("Facility updated: {FacilityId} Tenant={TenantId}", id, tenantId);

        _audit.Publish(
            eventType: "liens.facility.updated",
            action: "update",
            description: $"Facility '{ResolveFacilityName(entity)}' updated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Facility",
            entityId: entity.Id.ToString());

        return MapFacilityContactToResponse(entity);
    }

    public async Task<FacilityResponse> DeactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await ResolveFacilityContactAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Facility '{id}' not found.");

        entity.Deactivate(actingUserId);
        await _contactRepo.UpdateAsync(entity, ct);

        if (entity.FacilityId.HasValue)
        {
            var facilityEntity = await _facilityRepo.GetByIdAsync(tenantId, entity.FacilityId.Value, ct);
            if (facilityEntity is not null && facilityEntity.IsActive)
            {
                facilityEntity.Deactivate(actingUserId);
                await _facilityRepo.UpdateAsync(facilityEntity, ct);
            }
        }

        _logger.LogInformation("Facility deactivated: {FacilityId} Tenant={TenantId}", id, tenantId);

        _audit.Publish(
            eventType: "liens.facility.deactivated",
            action: "update",
            description: $"Facility '{ResolveFacilityName(entity)}' deactivated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Facility",
            entityId: entity.Id.ToString());

        return MapFacilityContactToResponse(entity);
    }

    // ── Contact-person operations ─────────────────────────────────────────────

    public async Task<List<FacilityContactPersonResponse>> GetContactPersonsAsync(
        Guid tenantId, Guid facilityId, CancellationToken ct = default)
    {
        var parentContact = await ResolveFacilityContactAsync(tenantId, facilityId, ct)
            ?? throw new NotFoundException($"Facility '{facilityId}' not found.");
        var linkedFacilityId = await EnsureBackingFacilityLinkAsync(parentContact, code: null, externalReference: null, actingUserId: null, ct);

        var items = await _contactRepo.GetByFacilityAsync(
            tenantId, linkedFacilityId, ContactSubtype.FacilityContactPerson, isActive: null, ct);
        return items.Select(x => MapPersonToResponse(x, parentContact.Id)).ToList();
    }

    public async Task<FacilityContactPersonResponse> CreateContactPersonAsync(
        Guid tenantId, Guid facilityId, Guid actingUserId,
        CreateFacilityContactPersonRequest request, CancellationToken ct = default)
    {
        var parentContact = await ResolveFacilityContactAsync(tenantId, facilityId, ct)
            ?? throw new NotFoundException($"Facility '{facilityId}' not found.");
        var linkedFacilityId = await EnsureBackingFacilityLinkAsync(parentContact, code: null, externalReference: null, actingUserId, ct);

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FirstName))
            errors["firstName"] = ["First name is required."];
        if (string.IsNullOrWhiteSpace(request.LastName))
            errors["lastName"] = ["Last name is required."];
        if (errors.Count > 0)
            throw new ValidationException("Required fields are missing.", errors);

        var entity = Contact.Create(
            tenantId, parentContact.OrgId, ContactType.Facility,
            request.FirstName, request.LastName, actingUserId,
            facilityId: linkedFacilityId,
            contactSubtype: ContactSubtype.FacilityContactPerson,
            title: request.Position,
            organization: ResolveFacilityName(parentContact),
            email: request.Email,
            phone: request.Phone);

        await _contactRepo.AddAsync(entity, ct);

        _logger.LogInformation("FacilityContactPerson created: {PersonId} Facility={FacilityId} Tenant={TenantId}",
            entity.Id, facilityId, tenantId);

        _audit.Publish(
            eventType: "liens.facility.contact_person.created",
            action: "create",
            description: $"Contact person '{entity.FirstName} {entity.LastName}' added to facility '{ResolveFacilityName(parentContact)}'",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "FacilityContactPerson",
            entityId: entity.Id.ToString());

        return MapPersonToResponse(entity, parentContact.Id);
    }

    public async Task<FacilityContactPersonResponse> UpdateContactPersonAsync(
        Guid tenantId, Guid facilityId, Guid personId, Guid actingUserId,
        UpdateFacilityContactPersonRequest request, CancellationToken ct = default)
    {
        var parentContact = await ResolveFacilityContactAsync(tenantId, facilityId, ct)
            ?? throw new NotFoundException($"Facility '{facilityId}' not found.");
        var linkedFacilityId = await EnsureBackingFacilityLinkAsync(parentContact, code: null, externalReference: null, actingUserId, ct);

        var entity = await _contactRepo.GetByIdAsync(tenantId, personId, ct)
            ?? throw new NotFoundException($"Contact person '{personId}' not found.");

        if (entity.FacilityId != linkedFacilityId || !IsFacilityContactPerson(entity))
            throw new NotFoundException($"Contact person '{personId}' does not belong to facility '{facilityId}'.");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FirstName))
            errors["firstName"] = ["First name is required."];
        if (string.IsNullOrWhiteSpace(request.LastName))
            errors["lastName"] = ["Last name is required."];
        if (errors.Count > 0)
            throw new ValidationException("Required fields are missing.", errors);

        entity.Update(
            request.FirstName,
            request.LastName,
            ContactType.Facility,
            actingUserId,
            facilityId: linkedFacilityId,
            contactSubtype: ContactSubtype.FacilityContactPerson,
            title: request.Position,
            organization: ResolveFacilityName(parentContact),
            email: request.Email,
            phone: request.Phone);

        await _contactRepo.UpdateAsync(entity, ct);

        _logger.LogInformation("FacilityContactPerson updated: {PersonId} Facility={FacilityId}", personId, linkedFacilityId);

        _audit.Publish(
            eventType: "liens.facility.contact_person.updated",
            action: "update",
            description: $"Contact person '{entity.FirstName} {entity.LastName}' updated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "FacilityContactPerson",
            entityId: entity.Id.ToString());

        return MapPersonToResponse(entity, parentContact.Id);
    }

    public async Task DeleteContactPersonAsync(
        Guid tenantId, Guid facilityId, Guid personId, Guid actingUserId, CancellationToken ct = default)
    {
        var parentContact = facilityId == Guid.Empty
            ? null
            : await ResolveFacilityContactAsync(tenantId, facilityId, ct);

        var entity = await _contactRepo.GetByIdAsync(tenantId, personId, ct)
            ?? throw new NotFoundException($"Contact person '{personId}' not found.");

        // facilityId == Guid.Empty when called from legacy route that has no facilityId in path
        if (!IsFacilityContactPerson(entity))
            throw new NotFoundException($"Contact person '{personId}' not found.");
        if (parentContact is not null)
        {
            var linkedFacilityId = await EnsureBackingFacilityLinkAsync(parentContact, code: null, externalReference: null, actingUserId, ct);
            if (entity.FacilityId != linkedFacilityId)
                throw new NotFoundException($"Contact person '{personId}' does not belong to facility '{facilityId}'.");
        }
        else if (facilityId != Guid.Empty && entity.FacilityId != facilityId)
            throw new NotFoundException($"Contact person '{personId}' does not belong to facility '{facilityId}'.");

        entity.Deactivate(actingUserId);
        await _contactRepo.UpdateAsync(entity, ct);

        _logger.LogInformation("FacilityContactPerson deactivated: {PersonId} Facility={FacilityId}", personId, entity.FacilityId);
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static FacilityResponse MapFacilityContactToResponse(Contact contact) => new()
    {
        Id                = contact.Id,
        Name              = ResolveFacilityName(contact),
        Code              = null,
        ExternalReference = null,
        AddressLine1      = contact.AddressLine1,
        AddressLine2      = null,
        City              = contact.City,
        State             = contact.State,
        PostalCode        = contact.PostalCode,
        Phone             = contact.Phone,
        Email             = contact.Email,
        Fax               = contact.Fax,
        IsActive          = contact.IsActive,
        CreatedAtUtc      = contact.CreatedAtUtc,
        UpdatedAtUtc      = contact.UpdatedAtUtc,
    };

    private static bool IsFacilityContactPerson(Contact c) =>
        (c.ContactType == ContactType.MedicalFacility || c.ContactType == ContactType.Facility)
        && c.ContactSubtype == ContactSubtype.FacilityContactPerson
        && c.FacilityId.HasValue;

    private static FacilityContactPersonResponse MapPersonToResponse(Contact p, Guid facilityReferenceId) => new()
    {
        Id           = p.Id,
        FacilityId   = facilityReferenceId,
        ContactType  = p.ContactType,
        ContactSubtype = p.ContactSubtype,
        FirstName    = p.FirstName,
        LastName     = p.LastName,
        Position     = p.Title,
        Email        = p.Email,
        Phone        = p.Phone,
        IsActive     = p.IsActive,
        CreatedAtUtc = p.CreatedAtUtc,
        UpdatedAtUtc = p.UpdatedAtUtc,
    };

    private async Task<Contact?> ResolveFacilityContactAsync(
        Guid tenantId,
        Guid facilityReferenceId,
        CancellationToken ct)
    {
        var contact = await _contactRepo.GetFacilityContactByReferenceAsync(tenantId, facilityReferenceId, ct);
        if (contact is not null)
            return contact;

        var facilityEntity = await _facilityRepo.GetByIdAsync(tenantId, facilityReferenceId, ct);
        if (facilityEntity is null)
            return null;

        contact = await _contactRepo.GetFacilityContactByNameAsync(tenantId, facilityEntity.Name, ct);
        if (contact is null)
            return null;

        if (contact.FacilityId != facilityEntity.Id)
        {
            contact.Update(
                contact.FirstName,
                contact.LastName,
                contact.ContactType,
                contact.UpdatedByUserId ?? Guid.Empty,
                facilityId: facilityEntity.Id,
                contactSubtype: contact.ContactSubtype,
                title: contact.Title,
                organization: contact.Organization,
                email: contact.Email,
                phone: contact.Phone,
                fax: contact.Fax,
                website: contact.Website,
                addressLine1: contact.AddressLine1,
                city: contact.City,
                state: contact.State,
                postalCode: contact.PostalCode,
                notes: contact.Notes);
            await _contactRepo.UpdateAsync(contact, ct);
        }

        return contact;
    }

    private async Task<Guid> EnsureBackingFacilityLinkAsync(
        Contact parentContact,
        string? code,
        string? externalReference,
        Guid? actingUserId,
        CancellationToken ct)
    {
        var userId = actingUserId
            ?? parentContact.UpdatedByUserId
            ?? parentContact.CreatedByUserId
            ?? throw new ValidationException("Unable to resolve acting user for facility linkage.",
                new Dictionary<string, string[]> { ["userId"] = ["Acting user is required for facility linkage."] });

        if (parentContact.FacilityId.HasValue)
        {
            var existing = await _facilityRepo.GetByIdAsync(parentContact.TenantId, parentContact.FacilityId.Value, ct);
            if (existing is not null)
            {
                existing.Update(
                    ResolveFacilityName(parentContact),
                    userId,
                    code,
                    externalReference,
                    parentContact.AddressLine1,
                    null,
                    parentContact.City,
                    parentContact.State,
                    parentContact.PostalCode,
                    parentContact.Phone,
                    parentContact.Email,
                    parentContact.Fax);
                await _facilityRepo.UpdateAsync(existing, ct);
                return existing.Id;
            }
        }

        var created = Facility.Create(
            parentContact.TenantId,
            parentContact.OrgId,
            ResolveFacilityName(parentContact),
            userId,
            code: code,
            externalReference: externalReference,
            addressLine1: parentContact.AddressLine1,
            addressLine2: null,
            city: parentContact.City,
            state: parentContact.State,
            postalCode: parentContact.PostalCode,
            phone: parentContact.Phone,
            email: parentContact.Email,
            fax: parentContact.Fax);

        await _facilityRepo.AddAsync(created, ct);

        if (parentContact.FacilityId != created.Id)
        {
            parentContact.Update(
                parentContact.FirstName,
                parentContact.LastName,
                parentContact.ContactType,
                userId,
                facilityId: created.Id,
                contactSubtype: parentContact.ContactSubtype,
                title: parentContact.Title,
                organization: parentContact.Organization,
                email: parentContact.Email,
                phone: parentContact.Phone,
                fax: parentContact.Fax,
                website: parentContact.Website,
                addressLine1: parentContact.AddressLine1,
                city: parentContact.City,
                state: parentContact.State,
                postalCode: parentContact.PostalCode,
                notes: parentContact.Notes);
            await _contactRepo.UpdateAsync(parentContact, ct);
        }

        return created.Id;
    }

    private static string ResolveFacilityName(Contact contact) =>
        !string.IsNullOrWhiteSpace(contact.Organization)
            ? contact.Organization.Trim()
            : contact.DisplayName;

    private static (string FirstName, string LastName) SplitFacilityName(string name)
    {
        var parts = name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => ("Facility", "Unknown"),
            1 => (parts[0], "Facility"),
            _ => (parts[0], parts[1]),
        };
    }
}
