using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;

namespace Liens.Api.Endpoints;

public static class LookupEndpoints
{
    public static void MapLookupEndpoints(this WebApplication app)
    {
        // ── v2 routes ─────────────────────────────────────────────────────────
        var group = app.MapGroup("/api/liens/lookups")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/categories", GetCategories)
            .RequirePermission(LiensPermissions.LienRead);

        group.MapGet("/all", GetAll)
            .RequirePermission(LiensPermissions.LienRead);

        group.MapGet("/{category}", GetByCategory)
            .RequirePermission(LiensPermissions.LienRead);

        group.MapGet("/{category}/{code}", GetByCode)
            .RequirePermission(LiensPermissions.LienRead);

        // ── Legacy routes (/lookup/*) ─────────────────────────────────────────
        var legacy = app.MapGroup("/lookup")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        // Reference data — map straight to existing LookupCategory values
        legacy.MapGet("/states",                  (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.State, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/document/type",           (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.DocumentCategory, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/accident/type",           (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.AccidentType, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/liens/status",            (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.LienStatus, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/case/status",             (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.CaseStatus, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/medical/status",          (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.MedicalStatus, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/settlement/status",       (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.SettlementStatus, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/settlement/type",         (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.SettlementType, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/current-attributes",      (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.CurrentAttributes, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/task/status",             (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.ServicingStatus, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/task/priority",           (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.ServicingPriority, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/contact/type",            (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.ContactType, ct))
            .RequirePermission(LiensPermissions.LienRead);

        // Procedure codes
        legacy.MapGet("/medical/procedure/codes", (ILookupValueService s, ICurrentRequestContext c, CancellationToken ct) => LegacyGetByCategory(s, c, LookupCategory.ProcedureCode, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/medical/procedure/costs/{code}", GetProcedureCost)
            .RequirePermission(LiensPermissions.LienRead);

        // Consolidated all-in-one lookup (legacy /lookup/all)
        legacy.MapGet("/all", GetAll)
            .RequirePermission(LiensPermissions.LienRead);

        // Contact-based lookups
        legacy.MapGet("/contact",                 (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactsByType(cs, c, null, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/contact/lawfirm",         (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactsByType(cs, c, ContactType.LawFirm, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/contact/medical-provider",(IContactService cs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactsByType(cs, c, ContactType.Provider, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/contact/funding-company", (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactsByType(cs, c, ContactType.LienHolder, ct))
            .RequirePermission(LiensPermissions.LienRead);

        // Law firm roles — returns case-manager contacts (closest equivalent to legacy "roles")
        legacy.MapGet("/contact/lawfirm/role",    (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactsByType(cs, c, ContactType.CaseManager, ct))
            .RequirePermission(LiensPermissions.LienRead);

        // Case managers by law-firm org (best-effort: returns all CaseManager contacts for tenant)
        legacy.MapGet("/backupcasemanager/{lawfirm}", (string lawfirm, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactsByType(cs, c, ContactType.CaseManager, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/casemanager/{lawfirm}",       (string lawfirm, IContactService cs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactsByType(cs, c, ContactType.CaseManager, ct))
            .RequirePermission(LiensPermissions.LienRead);

        // Contacts by role/type string
        legacy.MapGet("/contacts/{roleId}", GetContactsByRole)
            .RequirePermission(LiensPermissions.LienRead);

        // User list — not backed by a dedicated user-service yet; returns empty list
        legacy.MapGet("/user-list", (ICurrentRequestContext _) => Results.Ok(Array.Empty<object>()))
            .RequirePermission(LiensPermissions.LienRead);

        // Facility lookups — backed by IFacilityService (Phase 2)
        legacy.MapGet("/facility",                (IFacilityService fs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetAllFacilities(fs, c, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/contactperson/{id:guid}", (Guid id, IFacilityService fs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactPersons(id, fs, c, ct))
            .RequirePermission(LiensPermissions.LienRead);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetCategories(
        ILookupValueService lookupService,
        CancellationToken ct = default)
    {
        var categories = await lookupService.GetCategoriesAsync(ct);
        return Results.Ok(categories);
    }

    private static async Task<IResult> GetAll(
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var result = await lookupService.GetAllAsync(ctx.TenantId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByCategory(
        string category,
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        if (!LookupCategory.All.Contains(category))
            return Results.NotFound(new { error = new { code = "not_found", message = $"Category '{category}' is not a valid lookup category." } });

        var result = await lookupService.GetByCategoryAsync(ctx.TenantId, category, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByCode(
        string category,
        string code,
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        if (!LookupCategory.All.Contains(category))
            return Results.NotFound(new { error = new { code = "not_found", message = $"Category '{category}' is not a valid lookup category." } });

        var result = await lookupService.GetByCodeAsync(ctx.TenantId, category, code, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Lookup '{category}/{code}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetProcedureCost(
        string code,
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var result = await lookupService.GetByCodeAsync(ctx.TenantId, LookupCategory.ProcedureCode, code, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Procedure code '{code}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetContactsByRole(
        string roleId,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        // roleId is treated as a ContactType string (e.g. "LawFirm", "Provider")
        var contactType = ContactType.All.Contains(roleId) ? roleId : null;
        var result = await contactService.GetAllByTypeAsync(tenantId, contactType, isActive: true, ct);
        return Results.Ok(result);
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static async Task<IResult> LegacyGetByCategory(
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        string category,
        CancellationToken ct)
    {
        var result = await lookupService.GetByCategoryAsync(ctx.TenantId, category, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> LegacyGetContactsByType(
        IContactService contactService,
        ICurrentRequestContext ctx,
        string? contactType,
        CancellationToken ct)
    {
        var tenantId = ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var result = await contactService.GetAllByTypeAsync(tenantId, contactType, isActive: true, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> LegacyGetAllFacilities(
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var result = await facilityService.GetAllAsync(tenantId, isActive: true, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> LegacyGetContactPersons(
        Guid facilityId,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var result = await facilityService.GetContactPersonsAsync(tenantId, facilityId, ct);
        return Results.Ok(result);
    }
}
