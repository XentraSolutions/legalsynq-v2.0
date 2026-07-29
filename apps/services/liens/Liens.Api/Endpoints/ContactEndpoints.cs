using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

        group.MapGet("/{id:guid}/detail", GetContactDetail)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPost("/{id:guid}/reassign-cases", ReassignContactCases)
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
        group.MapGet("/medical-facilities", (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => ListByType(cs, c, ContactType.MedicalFacility, ct))
            .RequirePermission(LiensPermissions.LienService);
        group.MapGet("/lien-holders",    (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => ListByType(cs, c, ContactType.LienHolder, ct))
            .RequirePermission(LiensPermissions.LienService);
        group.MapGet("/funding-companies", ListFundingCompanies)
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
        group.MapPost("/medical-facilities/search", (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.MedicalFacility, ct))
            .RequirePermission(LiensPermissions.LienService);
        group.MapPost("/lien-holders/search", (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.LienHolder, ct))
            .RequirePermission(LiensPermissions.LienService);
        group.MapPost("/funding-companies/search", SearchFundingCompanies)
            .RequirePermission(LiensPermissions.LienService);
        group.MapPost("/leads/search",        (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.Lead, ct))
            .RequirePermission(LiensPermissions.LienService);

        // CSV export
        group.MapPost("/export-csv", ExportContactsCsv)
            .RequirePermission(LiensPermissions.LienService);
        group.MapPost("/generate-facility-csv", ExportFacilityCsv)
            .RequirePermission(LiensPermissions.LienService);

        // ── Legacy routes (/contact/*) ────────────────────────────────────────
        var legacy = app.MapGroup("/contact")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        legacy.MapPost("/create", LegacyCreateContact)
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/update", LegacyUpdateContact)
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapDelete("/delete/{id:guid}", DeactivateContact)
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapGet("/lawfirm/{id:guid?}",           (IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => LegacyListByType(cs, c, ContactType.LawFirm, id, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapGet("/medical-provider/{id:guid?}",  (IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => LegacyListByType(cs, c, ContactType.Provider, id, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapGet("/medical-facility/{id:guid?}",  (IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => LegacyListByType(cs, c, ContactType.MedicalFacility, id, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapGet("/funding-company/{id:guid?}",   LegacyListFundingCompanies)
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapGet("/leads/{id:guid?}",             (IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => LegacyListByType(cs, c, ContactType.Lead, id, ct))
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapPost("/lawfirm/v3",              (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.LawFirm, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/medical-provider/v3",     (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.Provider, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/medical-provider/v3/{id:guid?}", (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => SearchByType(r, cs, c, ContactType.Provider, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/medical-facility/v3",     (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.MedicalFacility, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/medical-facility/v3/{id:guid?}", (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => SearchByType(r, cs, c, ContactType.MedicalFacility, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/funding-company/v3",      SearchFundingCompanies)
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/funding-company/v3/{id:guid?}", (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => SearchFundingCompanies(r, cs, c, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/leads/v3",                (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => SearchByType(r, cs, c, ContactType.Lead, ct))
            .RequirePermission(LiensPermissions.LienService);
        legacy.MapPost("/leads/v3/{id:guid?}",     (ContactsV3Request r, IContactService cs, ICurrentRequestContext c, Guid? id, CancellationToken ct) => SearchByType(r, cs, c, ContactType.Lead, ct))
            .RequirePermission(LiensPermissions.LienService);

        legacy.MapGet("/lawfirm/role/{lawfirm:guid?}/{id:guid?}", () => Results.Ok(GetLawFirmRoleOptions()))
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
        HttpRequest request,
        string? search = null,
        string? contactType = null,
        Guid? lawFirmId = null,
        Guid? facilityId = null,
        string? type = null,
        string? contactSubtype = null,
        bool? isActive = true,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var isSubcontactQuery = lawFirmId.HasValue || facilityId.HasValue;
        var contactTypeIsSubtypeAlias = isSubcontactQuery &&
            !string.IsNullOrWhiteSpace(contactType) &&
            ContactSubtype.All.Contains(contactType.Trim());
        var typeIsSubtypeAlias = isSubcontactQuery &&
            !string.IsNullOrWhiteSpace(type) &&
            ContactSubtype.All.Contains(type.Trim());
        var hasExplicitContactSubtypeQuery = request.Query.Keys.Any(key =>
            string.Equals(key, "contactSubtype", StringComparison.OrdinalIgnoreCase));
        var explicitContactSubtype = hasExplicitContactSubtypeQuery && contactSubtype is null
            ? string.Empty
            : contactSubtype;
        var resolvedContactType = !string.IsNullOrWhiteSpace(contactType) && !contactTypeIsSubtypeAlias
            ? contactType
            : (!string.IsNullOrWhiteSpace(type) && !typeIsSubtypeAlias ? type : null);
        var resolvedContactSubtype = explicitContactSubtype is not null
            ? explicitContactSubtype
            : (contactTypeIsSubtypeAlias ? contactType : (typeIsSubtypeAlias ? type : null));
        var result = await contactService.SearchAsync(
            tenantId, search, resolvedContactType, isActive, page, pageSize, lawFirmId, facilityId, resolvedContactSubtype, ct);
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

    private static async Task<IResult> GetContactDetail(
        Guid id,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var contact = await db.Contacts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, ct);
        if (contact is null)
            return Results.NotFound(new { error = new { code = "not_found", message = $"Contact '{id}' not found." } });

        var relatedCases = await GetRelatedCasesQuery(db, tenantId, contact)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(100)
            .Select(item => new { item.Id, item.CaseNumber, item.Title, item.Status, item.UpdatedAtUtc })
            .ToListAsync(ct);
        var linkedLienCount = await db.Liens.AsNoTracking().CountAsync(lien =>
            lien.TenantId == tenantId &&
            (lien.FundingCompanyId == contact.Id || lien.FundingCompanyContactId == contact.Id), ct);

        return Results.Ok(new
        {
            contact = new
            {
                contact.Id,
                contact.ContactType,
                contact.ContactSubtype,
                contact.FirstName,
                contact.LastName,
                contact.DisplayName,
                contact.Organization,
                contact.Email,
                contact.Phone,
                contact.IsActive,
            },
            relatedCases,
            linkedLienCount,
        });
    }

    private static async Task<IResult> ReassignContactCases(
        Guid id,
        ReassignContactCasesRequest request,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var orgId = RequireOrgId(ctx);
        var source = await db.Contacts.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id && item.IsActive, ct);
        var target = await db.Contacts.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == request.TargetContactId && item.IsActive, ct);
        if (source is null || target is null)
            return Results.BadRequest(new { error = new { code = "invalid_contact", message = "Source and target contacts must exist in the current tenant." } });
        if (source.Id == target.Id)
            return Results.BadRequest(new { error = new { code = "same_contact", message = "Source and target contacts must differ." } });

        // Case reassignment is an organisation-scoped operation. A contact in
        // another org is neither an implicit delegate nor a valid destination;
        // cross-org transfers require a dedicated, audited workflow.
        if (source.OrgId != orgId || target.OrgId != orgId)
        {
            return Results.Forbid();
        }

        var relationship = request.RelationshipType?.Trim();
        if (!string.Equals(relationship, "CaseManager", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(relationship, "LawFirm", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = new { code = "invalid_relationship_type", message = "relationshipType must be CaseManager or LawFirm." } });

        var expectedType = string.Equals(relationship, "CaseManager", StringComparison.OrdinalIgnoreCase)
            ? ContactType.CaseManager
            : ContactType.LawFirm;
        if (!string.Equals(source.ContactType, expectedType, StringComparison.Ordinal) ||
            !string.Equals(target.ContactType, expectedType, StringComparison.Ordinal))
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "invalid_contact_relationship",
                    message = $"Both source and target contacts must be active {expectedType} contacts.",
                },
            });
        }

        var selectedScope = string.Equals(request.Scope, "Selected", StringComparison.OrdinalIgnoreCase);
        if (!selectedScope && !string.Equals(request.Scope, "All", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = new { code = "invalid_scope", message = "scope must be Selected or All." } });
        if (selectedScope && request.CaseIds.Count == 0)
            return Results.BadRequest(new { error = new { code = "case_ids_required", message = "caseIds is required for Selected scope." } });

        var sourceCases = GetRelatedCasesQuery(db, tenantId, source);
        if (selectedScope)
            sourceCases = sourceCases.Where(item => request.CaseIds.Contains(item.Id));
        var cases = await sourceCases.ToListAsync(ct);
        var results = new List<ReassignCaseResult>(cases.Count);
        foreach (var caseEntity in cases)
        {
            if (caseEntity.OrgId != orgId)
            {
                results.Add(new ReassignCaseResult(caseEntity.Id, false, "Case is not owned by the current organization."));
                continue;
            }

            if (string.Equals(relationship, "CaseManager", StringComparison.OrdinalIgnoreCase))
                caseEntity.ReassignCaseManager(target.Id, userId);
            else
                caseEntity.ReassignLawFirm(target.OrgId, userId);
            results.Add(new ReassignCaseResult(caseEntity.Id, true, null));
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(new
        {
            sourceContactId = source.Id,
            targetContactId = target.Id,
            relationshipType = relationship,
            requestedCount = cases.Count,
            reassignedCount = results.Count(result => result.Success),
            results,
        });
    }

    private static IQueryable<Case> GetRelatedCasesQuery(LiensDbContext db, Guid tenantId, Contact contact)
    {
        var query = db.Cases.Where(item => item.TenantId == tenantId);
        return contact.ContactType switch
        {
            ContactType.CaseManager => query.Where(item => item.Notes != null && item.Notes.Contains($"caseManagerId={contact.Id}")),
            ContactType.LawFirm => query.Where(item => item.OrgId == contact.OrgId || (item.Notes != null && item.Notes.Contains($"lawFirmId={contact.Id}"))),
            _ => query.Where(item => item.OrgId == contact.OrgId),
        };
    }

    private sealed record ReassignCaseResult(Guid CaseId, bool Success, string? Reason);

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

    private static async Task<IResult> LegacyCreateContact(
        LegacyCreateContactRequest request,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var (firstName, lastName) = ResolveLegacyCreateNames(request);

        var mappedRequest = new CreateContactRequest
        {
            ContactType = request.ContactType,
            ContactSubtype = request.ContactSubtype,
            FacilityId = request.FacilityId,
            LawFirmId = request.LawFirmId,
            FirstName = firstName,
            LastName = lastName,
            Title = request.Title,
            Organization = request.Organization,
            Email = request.Email,
            Phone = request.Phone,
            Fax = request.Fax,
            Website = request.Website,
            AddressLine1 = request.AddressLine1,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Notes = request.Notes,
        };

        var result = await contactService.CreateAsync(tenantId, orgId, userId, mappedRequest, ct);
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

    private static IReadOnlyList<object> GetLawFirmRoleOptions()
    {
        return new[]
        {
            new
            {
                code = ContactSubtype.LawFirmCaseManager,
                name = "Case Manager",
            },
            new
            {
                code = ContactSubtype.LawFirmAttorney,
                name = "Attorney",
            },
            new
            {
                code = ContactSubtype.LawFirmOther,
                name = "Other",
            },
        };
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

    private static Task<IResult> ListFundingCompanies(
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
        => ListByTypes(contactService, ctx, FundingCompanyContactTypes, ct);

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
            request.Page, request.Limit, ct: ct);
        return Results.Ok(result);
    }

    private static Task<IResult> SearchFundingCompanies(
        ContactsV3Request request,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
        => SearchByTypes(request, contactService, ctx, FundingCompanyContactTypes, ct);

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

    private static Task<IResult> LegacyListFundingCompanies(
        IContactService contactService,
        ICurrentRequestContext ctx,
        Guid? id,
        CancellationToken ct)
        => LegacyListByTypes(contactService, ctx, FundingCompanyContactTypes, id, ct);

    private static readonly string[] FundingCompanyContactTypes =
    [
        ContactType.LienHolder,
        ContactType.FundingCompany,
    ];

    private static async Task<IResult> ListByTypes(
        IContactService contactService,
        ICurrentRequestContext ctx,
        IReadOnlyCollection<string> contactTypes,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await GetCombinedContactsByTypesAsync(contactService, tenantId, contactTypes, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SearchByTypes(
        ContactsV3Request request,
        IContactService contactService,
        ICurrentRequestContext ctx,
        IReadOnlyCollection<string> contactTypes,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var allItems = await GetCombinedContactsByTypesAsync(contactService, tenantId, contactTypes, ct);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            allItems = allItems
                .Where(item => MatchesContactKeyword(item, keyword))
                .ToList();
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var limit = request.Limit < 1 ? 20 : request.Limit;

        return Results.Ok(new PaginatedResult<ContactResponse>
        {
            Items = allItems.Skip((page - 1) * limit).Take(limit).ToList(),
            Page = page,
            PageSize = limit,
            TotalCount = allItems.Count,
        });
    }

    private static async Task<IResult> LegacyListByTypes(
        IContactService contactService,
        ICurrentRequestContext ctx,
        IReadOnlyCollection<string> contactTypes,
        Guid? id,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        if (id.HasValue)
        {
            var single = await contactService.GetByIdAsync(tenantId, id.Value, ct);
            if (single is null || !contactTypes.Contains(single.ContactType))
                return Results.NotFound();

            return Results.Ok(new[] { single });
        }

        var all = await GetCombinedContactsByTypesAsync(contactService, tenantId, contactTypes, ct);
        return Results.Ok(all);
    }

    private static async Task<List<ContactResponse>> GetCombinedContactsByTypesAsync(
        IContactService contactService,
        Guid tenantId,
        IReadOnlyCollection<string> contactTypes,
        CancellationToken ct)
    {
        var batches = new List<ContactResponse>();
        foreach (var contactType in contactTypes)
        {
            var items = await contactService.GetAllByTypeAsync(tenantId, contactType, isActive: true, ct);
            batches.AddRange(items);
        }

        return batches
            .DistinctBy(item => item.Id)
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesContactKeyword(ContactResponse item, string keyword)
    {
        return item.FirstName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               item.LastName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               item.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(item.Email) && item.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(item.Organization) && item.Organization.Contains(keyword, StringComparison.OrdinalIgnoreCase));
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
        public string? ContactSubtype { get; init; }
        public Guid?   FacilityId  { get; init; }
        public Guid?   LawFirmId   { get; init; }
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

    private sealed class LegacyCreateContactRequest
    {
        public string  ContactType { get; init; } = string.Empty;
        public string? ContactSubtype { get; init; }
        public Guid?   FacilityId  { get; init; }
        public Guid?   LawFirmId   { get; init; }
        public string? FullName    { get; init; }
        public string? FirstName   { get; init; }
        public string? LastName    { get; init; }
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
            ContactType  = req.ContactType, ContactSubtype = req.ContactSubtype,
            FacilityId   = req.FacilityId, LawFirmId = req.LawFirmId,
            FirstName    = req.FirstName, LastName = req.LastName,
            Title        = req.Title, Organization = req.Organization,
            Email        = req.Email, Phone = req.Phone, Fax = req.Fax, Website = req.Website,
            AddressLine1 = req.AddressLine1, City = req.City, State = req.State,
            PostalCode   = req.PostalCode, Notes = req.Notes,
        };
        var result = await contactService.UpdateAsync(tenantId, req.Id, userId, request, ct);
        return Results.Ok(result);
    }

    private static (string FirstName, string LastName) ResolveLegacyCreateNames(LegacyCreateContactRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.FullName))
            return SplitLegacyFullName(request.FullName);

        return (request.FirstName ?? string.Empty, request.LastName ?? string.Empty);
    }

    private static (string FirstName, string LastName) SplitLegacyFullName(string fullName)
    {
        var parts = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return (string.Empty, string.Empty);

        if (parts.Length == 1)
            return (parts[0], string.Empty);

        return (string.Join(" ", parts[..^1]), parts[^1]);
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
