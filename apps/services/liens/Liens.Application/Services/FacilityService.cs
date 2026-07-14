using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class FacilityService : IFacilityService
{
    private readonly IFacilityRepository _facilityRepo;
    private readonly IFacilityContactPersonRepository _personRepo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<FacilityService> _logger;

    public FacilityService(
        IFacilityRepository facilityRepo,
        IFacilityContactPersonRepository personRepo,
        IAuditPublisher audit,
        ILogger<FacilityService> logger)
    {
        _facilityRepo = facilityRepo;
        _personRepo   = personRepo;
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

        var (items, totalCount) = await _facilityRepo.SearchAsync(
            tenantId, search, isActive, page, pageSize, ct);

        return new PaginatedResult<FacilityResponse>
        {
            Items      = items.Select(MapToResponse).ToList(),
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<List<FacilityResponse>> GetAllAsync(
        Guid tenantId, bool? isActive = true, CancellationToken ct = default)
    {
        var (items, _) = await _facilityRepo.SearchAsync(
            tenantId, search: null, isActive, page: 1, pageSize: 10_000, ct);
        return items.Select(MapToResponse).ToList();
    }

    public async Task<FacilityResponse?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _facilityRepo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<FacilityResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateFacilityRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Facility name is required.",
                new Dictionary<string, string[]> { ["name"] = ["Facility name is required."] });

        var entity = Facility.Create(
            tenantId, orgId, request.Name, actingUserId,
            request.Code, request.ExternalReference,
            request.AddressLine1, request.AddressLine2,
            request.City, request.State, request.PostalCode,
            request.Phone, request.Email, request.Fax);

        await _facilityRepo.AddAsync(entity, ct);

        _logger.LogInformation("Facility created: {FacilityId} '{Name}' Tenant={TenantId}",
            entity.Id, entity.Name, tenantId);

        _audit.Publish(
            eventType: "liens.facility.created",
            action: "create",
            description: $"Facility '{entity.Name}' created",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Facility",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<FacilityResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateFacilityRequest request, CancellationToken ct = default)
    {
        var entity = await _facilityRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Facility '{id}' not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Facility name is required.",
                new Dictionary<string, string[]> { ["name"] = ["Facility name is required."] });

        entity.Update(
            request.Name, actingUserId,
            request.Code, request.ExternalReference,
            request.AddressLine1, request.AddressLine2,
            request.City, request.State, request.PostalCode,
            request.Phone, request.Email, request.Fax);

        await _facilityRepo.UpdateAsync(entity, ct);

        _logger.LogInformation("Facility updated: {FacilityId} Tenant={TenantId}", id, tenantId);

        _audit.Publish(
            eventType: "liens.facility.updated",
            action: "update",
            description: $"Facility '{entity.Name}' updated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Facility",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<FacilityResponse> DeactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await _facilityRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Facility '{id}' not found.");

        entity.Deactivate(actingUserId);
        await _facilityRepo.UpdateAsync(entity, ct);

        _logger.LogInformation("Facility deactivated: {FacilityId} Tenant={TenantId}", id, tenantId);

        _audit.Publish(
            eventType: "liens.facility.deactivated",
            action: "update",
            description: $"Facility '{entity.Name}' deactivated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Facility",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    // ── Contact-person operations ─────────────────────────────────────────────

    public async Task<List<FacilityContactPersonResponse>> GetContactPersonsAsync(
        Guid tenantId, Guid facilityId, CancellationToken ct = default)
    {
        var items = await _personRepo.GetByFacilityAsync(tenantId, facilityId, ct);
        return items.Select(MapPersonToResponse).ToList();
    }

    public async Task<FacilityContactPersonResponse> CreateContactPersonAsync(
        Guid tenantId, Guid facilityId, Guid actingUserId,
        CreateFacilityContactPersonRequest request, CancellationToken ct = default)
    {
        var facility = await _facilityRepo.GetByIdAsync(tenantId, facilityId, ct)
            ?? throw new NotFoundException($"Facility '{facilityId}' not found.");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FirstName))
            errors["firstName"] = ["First name is required."];
        if (string.IsNullOrWhiteSpace(request.LastName))
            errors["lastName"] = ["Last name is required."];
        if (errors.Count > 0)
            throw new ValidationException("Required fields are missing.", errors);

        var entity = FacilityContactPerson.Create(
            tenantId, facilityId,
            request.FirstName, request.LastName, actingUserId,
            request.Position, request.Email, request.Phone);

        await _personRepo.AddAsync(entity, ct);

        _logger.LogInformation("FacilityContactPerson created: {PersonId} Facility={FacilityId} Tenant={TenantId}",
            entity.Id, facilityId, tenantId);

        _audit.Publish(
            eventType: "liens.facility.contact_person.created",
            action: "create",
            description: $"Contact person '{entity.FirstName} {entity.LastName}' added to facility '{facility.Name}'",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "FacilityContactPerson",
            entityId: entity.Id.ToString());

        return MapPersonToResponse(entity);
    }

    public async Task<FacilityContactPersonResponse> UpdateContactPersonAsync(
        Guid tenantId, Guid facilityId, Guid personId, Guid actingUserId,
        UpdateFacilityContactPersonRequest request, CancellationToken ct = default)
    {
        var entity = await _personRepo.GetByIdAsync(tenantId, personId, ct)
            ?? throw new NotFoundException($"Contact person '{personId}' not found.");

        if (entity.FacilityId != facilityId)
            throw new NotFoundException($"Contact person '{personId}' does not belong to facility '{facilityId}'.");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FirstName))
            errors["firstName"] = ["First name is required."];
        if (string.IsNullOrWhiteSpace(request.LastName))
            errors["lastName"] = ["Last name is required."];
        if (errors.Count > 0)
            throw new ValidationException("Required fields are missing.", errors);

        entity.Update(request.FirstName, request.LastName, actingUserId,
            request.Position, request.Email, request.Phone);

        await _personRepo.UpdateAsync(entity, ct);

        _logger.LogInformation("FacilityContactPerson updated: {PersonId} Facility={FacilityId}", personId, facilityId);

        _audit.Publish(
            eventType: "liens.facility.contact_person.updated",
            action: "update",
            description: $"Contact person '{entity.FirstName} {entity.LastName}' updated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "FacilityContactPerson",
            entityId: entity.Id.ToString());

        return MapPersonToResponse(entity);
    }

    public async Task DeleteContactPersonAsync(
        Guid tenantId, Guid facilityId, Guid personId, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await _personRepo.GetByIdAsync(tenantId, personId, ct)
            ?? throw new NotFoundException($"Contact person '{personId}' not found.");

        // facilityId == Guid.Empty when called from legacy route that has no facilityId in path
        if (facilityId != Guid.Empty && entity.FacilityId != facilityId)
            throw new NotFoundException($"Contact person '{personId}' does not belong to facility '{facilityId}'.");

        entity.Deactivate(actingUserId);
        await _personRepo.UpdateAsync(entity, ct);

        _logger.LogInformation("FacilityContactPerson deactivated: {PersonId} Facility={FacilityId}", personId, entity.FacilityId);
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static FacilityResponse MapToResponse(Facility f) => new()
    {
        Id                = f.Id,
        Name              = f.Name,
        Code              = f.Code,
        ExternalReference = f.ExternalReference,
        AddressLine1      = f.AddressLine1,
        AddressLine2      = f.AddressLine2,
        City              = f.City,
        State             = f.State,
        PostalCode        = f.PostalCode,
        Phone             = f.Phone,
        Email             = f.Email,
        Fax               = f.Fax,
        IsActive          = f.IsActive,
        CreatedAtUtc      = f.CreatedAtUtc,
        UpdatedAtUtc      = f.UpdatedAtUtc,
    };

    private static FacilityContactPersonResponse MapPersonToResponse(FacilityContactPerson p) => new()
    {
        Id           = p.Id,
        FacilityId   = p.FacilityId,
        FirstName    = p.FirstName,
        LastName     = p.LastName,
        Position     = p.Position,
        Email        = p.Email,
        Phone        = p.Phone,
        IsActive     = p.IsActive,
        CreatedAtUtc = p.CreatedAtUtc,
        UpdatedAtUtc = p.UpdatedAtUtc,
    };
}
