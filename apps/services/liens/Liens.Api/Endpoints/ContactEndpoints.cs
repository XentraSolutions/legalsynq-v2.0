using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;
using System.Text;

namespace Liens.Api.Endpoints;

public static class ContactEndpoints
{
    public static void MapContactEndpoints(this WebApplication app)
    {
        // ── v2 routes ─────────────────────────────────────────────────────────
        var group = app.MapGroup("/api/liens/contacts")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/", ListContacts)
            .RequirePermission(LiensPermissions.LienService);

        group.MapGet("/{id:guid}", GetContactById)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPost("/", CreateContact)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPut("/{id:guid}", UpdateContact)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPut("/{id:guid}/deactivate", DeactivateContact)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPut("/{id:guid}/reactivate", ReactivateContact)
            .RequirePermission(LiensPermissions.LienService);

        // Typed list routes
        group.MapGet("/law-firms",       (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => ListByType(cs, c, ContactType.LawFirm, ct))
            .RequirePermission(LiensPermissions.LienService);
        group.MapGet("/providers",       (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => ListByType(cs, c, ContactType.Provider, ct))
            .RequirePermission(LiensPermissions.LienService);
        group.MapGet("/lien-holders",    (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => ListByType(cs, c, ContactType.LienHolder, ct))
            .RequirePermission(LiensPermissions.LienService);
        group.MapGet("/leads",           (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => ListByType(cs, c, ContactType.Lead, ct))
            .RequirePermission(LiensPermissions.LienService);
        group.MapGet("/case-managers",   (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => ListByType(cs, c, ContactType.CaseManager, ct))
            .RequirePermission(LiensPermissions.LienService);

        // Paginated typed search
        group.MapPost("/law-firms/search",    (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.LawFirm, ct))
            .RequirePermission(LiensPermissions.LienService);
        group.MapPost("/providers/search",    (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.Provider, ct))
            .RequirePermission(LiensPermissions.LienService);
        group.MapPost("/lien-holders/search", (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.LienHolder, ct))
            .RequirePermission(LiensPermissions.LienService);
        group.MapPost("/leads/search",        (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.Lead, ct))
            .RequirePermission(LiensPermissions.LienService);

        // CSV export
        group.MapPost("/export-csv", ExportContactsCsv)
            .RequirePermission(LiensPermissions.LienService);

        // ── Legacy routes (/contact/*) ────────────────────────────────────────
        var legacy = app.MapGroup("/contact")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        legacy.MapPost("/create", CreateContact)
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/update", LegacyUpdateContact)
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapDelete("/delete/{id:guid}", DeactivateContact)
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapGet("/lawfirm/{id:guid?}",           (IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => LegacyListByType(cs, c, ContactType.LawFirm, id, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapGet("/medical-provider/{id:guid?}",  (IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => LegacyListByType(cs, c, ContactType.Provider, id, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapGet("/funding-company/{id:guid?}",   (IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => LegacyListByType(cs, c, ContactType.LienHolder, id, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapGet("/leads/{id:guid?}",             (IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => LegacyListByType(cs, c, ContactType.Lead, id, ct))
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapPost("/lawfirm/v3",              (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.LawFirm, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/medical-provider/v3",     (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.Provider, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/medical-provider/v3/{id:guid?}", (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => SearchByType(r, cs, c, ContactType.Provider, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/funding-company/v3",      (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.LienHolder, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/funding-company/v3/{id:guid?}", (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => SearchByType(r, cs, c, ContactType.LienHolder, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/leads/v3",                (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.Lead, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/leads/v3/{id:guid?}",     (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => SearchByType(r, cs, c, ContactType.Lead, ct))
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapGet("/lawfirm/role/{lawfirm:guid?}/{id:guid?}", (IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => ListByType(cs, c, ContactType.CaseManager, ct))
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapPost("/generate-csv",          ExportContactsCsv)
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/generate-facility-csv", ExportFacilityCsv)
            .RequirePermission(LiensPermissions.LienService);
    }

    // ── Context helpers ───────────────────────────────────────────────────────

    private static Guid RequireTenantId(ICurrentRequestContext ctx)
    {
        return ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");
    }

    private static Guid RequireUserId(ICurrentRequestContext ctx)
    {
        return ctx.UserId
            ?? throw new UnauthorizedAccessException("User context is required.");
    }

    private static Guid RequireOrgId(ICurrentRequestContext ctx)
    {
        return ctx.OrgId
            ?? throw new UnauthorizedAccessException("Organization context is required.");
    }

    private static async Task<IResult> ListContacts(
        IContactService contactService,
        ICurrentRequestContext ctx,
        string? search = null,
        string? contactType = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await contactService.SearchAsync(
            tenantId, search, contactType, isActive, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetContactById(
        Guid id,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await contactService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Contact '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> CreateContact(
        CreateContactRequest request,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await contactService.CreateAsync(tenantId, orgId, userId, request, ct);
        return Results.Created($"/api/liens/contacts/{result.Id}", result);
    }

    private static async Task<IResult> UpdateContact(
        Guid id,
        UpdateContactRequest request,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await contactService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeactivateContact(
        Guid id,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await contactService.DeactivateAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ReactivateContact(
        Guid id,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await contactService.ReactivateAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    // ── Typed-list handlers ───────────────────────────────────────────────────

    private static async Task<IResult> ListByType(
        IContactService contactService,
        ICurrentRequestContext ctx,
        string contactType,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await contactService.GetAllByTypeAsync(tenantId, contactType, isActive: true, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SearchByType(
        ContactsV3Request request,
        IContactService contactService,
        ICurrentRequestContext ctx,
        string contactType,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await contactService.SearchAsync(
            tenantId, request.Keyword, contactType, isActive: true,
            request.Page, request.Limit, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> LegacyListByType(
        IContactService contactService,
        ICurrentRequestContext ctx,
        string contactType,
        Guid? id,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        if (id.HasValue)
        {
            var single = await contactService.GetByIdAsync(tenantId, id.Value, ct);
            return single is null ? Results.NotFound() : Results.Ok(new[] { single });
        }
        var all = await contactService.GetAllByTypeAsync(tenantId, contactType, isActive: true, ct);
        return Results.Ok(all);
    }

    // ── CSV export handlers ───────────────────────────────────────────────────

    private static async Task<IResult> ExportContactsCsv(
        ContactCsvRequest request,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var contacts = await contactService.GetAllByTypeAsync(
            tenantId, request.ContactType, isActive: null, ct);

        var csv = BuildContactCsv(contacts);
        return Results.Ok(new { data = csv });
    }

    private static Task<IResult> ExportFacilityCsv(
        FacilityCsvRequest request,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        // Facility CSV export — returns a stub; full implementation requires IFacilityService
        return Task.FromResult(Results.Ok(new { data = string.Empty }));
    }

    private static string BuildContactCsv(List<ContactResponse> contacts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ContactType,FirstName,LastName,DisplayName,Email,Phone,Organization,City,State,IsActive");
        foreach (var c in contacts)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(c.ContactType), CsvEscape(c.FirstName), CsvEscape(c.LastName),
                CsvEscape(c.DisplayName), CsvEscape(c.Email), CsvEscape(c.Phone),
                CsvEscape(c.Organization), CsvEscape(c.City), CsvEscape(c.State),
                c.IsActive ? "1" : "0"));
        }
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static string CsvEscape(string? value)
    {
        if (value is null) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    // ── Legacy update shim (body contains id) ────────────────────────────────

    private sealed class LegacyUpdateContactRequest
    {
        public Guid    Id          { get; init; }
        public string  ContactType { get; init; } = string.Empty;
        public string  FirstName   { get; init; } = string.Empty;
        public string  LastName    { get; init; } = string.Empty;
        public string? Title       { get; init; }
        public string? Organization{ get; init; }
        public string? Email       { get; init; }
        public string? Phone       { get; init; }
        public string? Fax         { get; init; }
        public string? Website     { get; init; }
        public string? AddressLine1{ get; init; }
        public string? City        { get; init; }
        public string? State       { get; init; }
        public string? PostalCode  { get; init; }
        public string? Notes       { get; init; }
    }

    private static async Task<IResult> LegacyUpdateContact(
        LegacyUpdateContactRequest req,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var request  = new UpdateContactRequest
        {
            ContactType  = req.ContactType, FirstName = req.FirstName, LastName = req.LastName,
            Title        = req.Title, Organization = req.Organization,
            Email        = req.Email, Phone = req.Phone, Fax = req.Fax, Website = req.Website,
            AddressLine1 = req.AddressLine1, City = req.City, State = req.State,
            PostalCode   = req.PostalCode, Notes = req.Notes,
        };
        var result = await contactService.UpdateAsync(tenantId, req.Id, userId, request, ct);
        return Results.Ok(result);
    }

    // ── Request shims ─────────────────────────────────────────────────────────

    private sealed class ContactsV3Request
    {
        public string? Keyword { get; init; }
        public int     Page    { get; init; } = 1;
        public int     Limit   { get; init; } = 20;
    }

    private sealed class ContactCsvRequest
    {
        public string? ContactType { get; init; }
    }

    private sealed class FacilityCsvRequest
    {
        public Guid? Id { get; init; }
    }
}
