using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

public static class FacilityEndpoints
{
    public static void MapFacilityEndpoints(this WebApplication app)
    {
        // ── v2 routes ─────────────────────────────────────────────────────────
        var group = app.MapGroup("/api/liens/facilities")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/", ListFacilities)
            .RequirePermission(LiensPermissions.LienService);

        group.MapGet("/{id:guid}", GetFacilityById)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPost("/", CreateFacility)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPost("/v3", SearchFacilities)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPut("/{id:guid}", UpdateFacility)
            .RequirePermission(LiensPermissions.LienService);

        group.MapDelete("/{id:guid}", DeactivateFacility)
            .RequirePermission(LiensPermissions.LienService);

        // Contact persons
        group.MapGet("/{id:guid}/contact-persons", ListContactPersons)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPost("/{id:guid}/contact-persons", CreateContactPerson)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPut("/{id:guid}/contact-persons/{personId:guid}", UpdateContactPerson)
            .RequirePermission(LiensPermissions.LienService);

        group.MapDelete("/{id:guid}/contact-persons/{personId:guid}", DeleteContactPerson)
            .RequirePermission(LiensPermissions.LienService);

        // ── Legacy routes (/facility/*) ───────────────────────────────────────
        var legacy = app.MapGroup("/facility")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        legacy.MapPost("/create", CreateFacility)
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapPost("/update", LegacyUpdateFacility)
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapDelete("/delete/{id:guid}", DeactivateFacility)
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapGet("/list/{id:guid?}", LegacyListFacilities)
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapPost("/list/v3", SearchFacilities)
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapPost("/contactperson", LegacyCreateContactPerson)
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapPost("/update-contactperson", LegacyUpdateContactPerson)
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapDelete("/delete-contactperson/{id:guid}", LegacyDeleteContactPerson)
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapGet("/get-contactperson/{id:guid}", LegacyGetContactPersonsByFacility)
            .RequirePermission(LiensPermissions.LienService);
    }

    // ── Context helpers ───────────────────────────────────────────────────────

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireOrgId(ICurrentRequestContext ctx) =>
        ctx.OrgId ?? throw new UnauthorizedAccessException("Organization context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> ListFacilities(
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        string? search = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await facilityService.SearchAsync(tenantId, search, isActive, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SearchFacilities(
        FacilitySearchRequest request,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await facilityService.SearchAsync(
            tenantId, request.Keyword, request.IsActive,
            request.Page, request.Limit, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetFacilityById(
        Guid id,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await facilityService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Facility '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> CreateFacility(
        CreateFacilityRequest request,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId    = RequireOrgId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await facilityService.CreateAsync(tenantId, orgId, userId, request, ct);
        return Results.Created($"/api/liens/facilities/{result.Id}", result);
    }

    private static async Task<IResult> UpdateFacility(
        Guid id,
        UpdateFacilityRequest request,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await facilityService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeactivateFacility(
        Guid id,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        await facilityService.DeactivateAsync(tenantId, id, userId, ct);
        return Results.NoContent();
    }

    // ── Contact-person handlers ───────────────────────────────────────────────

    private static async Task<IResult> ListContactPersons(
        Guid id,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await facilityService.GetContactPersonsAsync(tenantId, id, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateContactPerson(
        Guid id,
        CreateFacilityContactPersonRequest request,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await facilityService.CreateContactPersonAsync(tenantId, id, userId, request, ct);
        return Results.Created($"/api/liens/facilities/{id}/contact-persons/{result.Id}", result);
    }

    private static async Task<IResult> UpdateContactPerson(
        Guid id,
        Guid personId,
        UpdateFacilityContactPersonRequest request,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await facilityService.UpdateContactPersonAsync(tenantId, id, personId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteContactPerson(
        Guid id,
        Guid personId,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        await facilityService.DeleteContactPersonAsync(tenantId, id, personId, userId, ct);
        return Results.NoContent();
    }

    // ── Legacy shims ──────────────────────────────────────────────────────────

    private sealed class LegacyUpdateFacilityRequest
    {
        public Guid    Id               { get; init; }
        public string  Name             { get; init; } = string.Empty;
        public string? Code             { get; init; }
        public string? ExternalReference{ get; init; }
        public string? AddressLine1     { get; init; }
        public string? AddressLine2     { get; init; }
        public string? City             { get; init; }
        public string? State            { get; init; }
        public string? PostalCode       { get; init; }
        public string? Phone            { get; init; }
        public string? Email            { get; init; }
        public string? Fax              { get; init; }
    }

    private static async Task<IResult> LegacyUpdateFacility(
        LegacyUpdateFacilityRequest req,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var request  = new UpdateFacilityRequest
        {
            Name = req.Name, Code = req.Code, ExternalReference = req.ExternalReference,
            AddressLine1 = req.AddressLine1, AddressLine2 = req.AddressLine2,
            City = req.City, State = req.State, PostalCode = req.PostalCode,
            Phone = req.Phone, Email = req.Email, Fax = req.Fax,
        };
        var result = await facilityService.UpdateAsync(tenantId, req.Id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> LegacyListFacilities(
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        Guid? id = null,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        if (id.HasValue)
        {
            var single = await facilityService.GetByIdAsync(tenantId, id.Value, ct);
            return single is null
                ? Results.NotFound()
                : Results.Ok(new[] { single });
        }
        var all = await facilityService.GetAllAsync(tenantId, isActive: null, ct);
        return Results.Ok(all);
    }

    // Legacy contact-person payloads use a flat facilityId field
    private sealed class LegacyContactPersonRequest
    {
        public Guid    FacilityId { get; init; }
        public string  FirstName  { get; init; } = string.Empty;
        public string  LastName   { get; init; } = string.Empty;
        public string? Position   { get; init; }
        public string? Email      { get; init; }
        public string? Phone      { get; init; }
    }

    private sealed class LegacyUpdateContactPersonRequest
    {
        public Guid    Id         { get; init; }
        public Guid    FacilityId { get; init; }
        public string  FirstName  { get; init; } = string.Empty;
        public string  LastName   { get; init; } = string.Empty;
        public string? Position   { get; init; }
        public string? Email      { get; init; }
        public string? Phone      { get; init; }
    }

    private static async Task<IResult> LegacyCreateContactPerson(
        LegacyContactPersonRequest req,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var request  = new CreateFacilityContactPersonRequest
        {
            FirstName = req.FirstName, LastName = req.LastName,
            Position  = req.Position,  Email    = req.Email, Phone = req.Phone,
        };
        var result = await facilityService.CreateContactPersonAsync(tenantId, req.FacilityId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> LegacyUpdateContactPerson(
        LegacyUpdateContactPersonRequest req,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var request  = new UpdateFacilityContactPersonRequest
        {
            FirstName = req.FirstName, LastName = req.LastName,
            Position  = req.Position,  Email    = req.Email, Phone = req.Phone,
        };
        var result = await facilityService.UpdateContactPersonAsync(tenantId, req.FacilityId, req.Id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> LegacyDeleteContactPerson(
        Guid id,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        // Legacy route doesn't carry facilityId in path; look up the person and resolve
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        // We pass Guid.Empty as facilityId — service resolves the entity by personId
        await facilityService.DeleteContactPersonAsync(tenantId, Guid.Empty, id, userId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> LegacyGetContactPersonsByFacility(
        Guid id,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await facilityService.GetContactPersonsAsync(tenantId, id, ct);
        return Results.Ok(result);
    }

    // ── Request shim used by POST /v3 ─────────────────────────────────────────
    private sealed class FacilitySearchRequest
    {
        public string? Keyword  { get; init; }
        public bool?   IsActive { get; init; }
        public int     Page     { get; init; } = 1;
        public int     Limit    { get; init; } = 20;
    }
}
