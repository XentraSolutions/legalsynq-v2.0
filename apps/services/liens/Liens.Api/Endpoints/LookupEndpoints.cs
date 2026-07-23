using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Liens.Api.Endpoints;

public static class LookupEndpoints
{
    private const string MedicareProcedureLookupClientName = "MedicareProcedureLookup";

    // Temporary testing credentials requested for the CMS Procedure Price Lookup API.
    // Replace with configuration/secret-store values before promoting beyond test use.
    private const string MedicareProcedureLookupApiKey = "1iuNYl3IYBHTSjmn34m0XOLLqfm1nrmz";
    private const string MedicareProcedureLookupAmaLicense = "b733fd32-ee85-4174-9ab1-e09ec14048bb";

    private static readonly JsonSerializerOptions MedicareJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly (string Code, string Name, string[] SourceCodes, int SortOrder)[] LegacyContactTypeOptions =
    [
        (ContactType.LawFirm, "Law Firms", [ContactType.LawFirm], 1),
        (ContactType.Provider, "Medical Providers", [ContactType.Provider], 2),
        (ContactType.FundingCompany, "Funding Companies", [ContactType.FundingCompany, ContactType.LienHolder], 3),
        (ContactType.MedicalFacility, "Medical Facilities", [ContactType.MedicalFacility, ContactType.Facility], 4),
        (ContactType.Lead, "Leads", [ContactType.Lead], 5),
    ];

    private static readonly (string Code, string Name, string[] SourceCodes, int SortOrder)[] LegacyCaseStatusOptions =
    [
        ("New", "New", [CaseStatus.PreDemand], 1),
        ("Processing", "Processing", [CaseStatus.PreDemand], 2),
        (CaseStatus.Closed, "Closed", [CaseStatus.Closed], 3),
        (CaseStatus.PreDemand, "Pre-demand", [CaseStatus.PreDemand], 4),
        (CaseStatus.DemandSent, "Demand Sent", [CaseStatus.DemandSent], 5),
        ("Negotiations", "Negotiations", [CaseStatus.InNegotiation], 6),
        ("Litigation", "Litigation", [CaseStatus.InNegotiation], 7),
        (CaseStatus.CaseSettled, "Case Settled", [CaseStatus.CaseSettled], 8),
    ];

    private static readonly (string Code, string Name, string[] SourceCodes, int SortOrder)[] LegacyAccidentTypeOptions =
    [
        ("DogBite", "Dog Bite", ["DogBite"], 1),
        ("MotorVehicleAccident", "Motor Vehicle Accident", ["MotorVehicleAccident", "MVA"], 2),
        ("Other", "Other", ["Other"], 3),
        ("SlipAndFall", "Slip and Fall", ["SlipAndFall"], 4),
        ("WorkersCompensation", "Workers Compensation", ["WorkersCompensation"], 5),
        ("MedicalMalpractice", "Medical Malpractice", [], 6),
    ];

    private static readonly (string Code, string Name, string[] SourceCodes, int SortOrder)[] LegacyLienStatusOptions =
    [
        ("Open", "Open", [LienStatus.Draft, LienStatus.Offered, LienStatus.Accepted, LienStatus.UnderReview, LienStatus.Sold, LienStatus.Active, LienStatus.Disputed], 1),
        ("Closed", "Closed", [LienStatus.Settled], 2),
        ("Rejected", "Rejected", [LienStatus.Declined, LienStatus.Withdrawn, LienStatus.Cancelled], 3),
    ];

    private static readonly (Guid FallbackId, string Code, string Name, string[] SourceCodes, int SortOrder)[] LegacyDocumentTypeOptions =
    [
        (Guid.Parse("10000000-0000-0000-0000-000000000001"), "HicfaOrBill", "HICFA or Bill", [], 1),
        (Guid.Parse("10000000-0000-0000-0000-000000000002"), "MedicalRecord", "Medical Record", ["MedicalRecord"], 2),
        (Guid.Parse("10000000-0000-0000-0000-000000000003"), "HIPPA", "HIPPA", [], 3),
        (Guid.Parse("10000000-0000-0000-0000-000000000004"), "PoliceReport", "Police Report", [], 4),
        (Guid.Parse("10000000-0000-0000-0000-000000000005"), "Other", "Other", ["Other"], 5),
        (Guid.Parse("10000000-0000-0000-0000-000000000006"), "LienAgreement", "Lien Agreement", ["LienAgreement"], 6),
        (Guid.Parse("10000000-0000-0000-0000-000000000007"), "Check", "Check", ["CheckDocument"], 7),
        (Guid.Parse("10000000-0000-0000-0000-000000000008"), "AddTestQA", "Add Test QA", [], 8),
        (Guid.Parse("10000000-0000-0000-0000-000000000009"), "BillsAndRecords", "Bills & Records", [], 9),
        (Guid.Parse("10000000-0000-0000-0000-000000000010"), "BillsAndRecs", "Bills & Recs", [], 10),
    ];

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
        legacy.MapGet("/document/type",           GetLegacyDocumentTypes)
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/accident/type",           GetLegacyAccidentTypes)
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/liens/status",            GetLegacyLienStatuses)
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/case/status",             GetLegacyCaseStatuses)
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
        legacy.MapGet("/contact/type",            GetLegacyContactTypes)
            .RequirePermission(LiensPermissions.LienRead);

        // Procedure codes
        legacy.MapGet("/medical/procedure/codes", GetLegacyProcedureCodes)
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/medical/procedure/costs/{code}", GetProcedureCost)
            .RequirePermission(LiensPermissions.LienRead);

        // Consolidated all-in-one lookup (legacy /lookup/all)
        legacy.MapGet("/all", GetAll)
            .RequirePermission(LiensPermissions.LienRead);

        // Contact-based lookups
        legacy.MapGet("/contact",(IContactService cs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactsByType(cs, c, null, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/contact/lawfirm",         (IContactService cs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactsByType(cs, c, ContactType.LawFirm, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/contact/medical-provider",(IContactService cs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactsByType(cs, c, ContactType.Provider, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/contact/medical-facility",(IContactService cs, ICurrentRequestContext c, CancellationToken ct) => LegacyGetContactsByType(cs, c, ContactType.MedicalFacility, ct))
            .RequirePermission(LiensPermissions.LienRead);
        legacy.MapGet("/contact/funding-company", LegacyGetFundingCompanies)
            .RequirePermission(LiensPermissions.LienRead);

        // Law firm roles — exposed as law-firm contact subtype options.
        legacy.MapGet("/contact/lawfirm/role",    () => Results.Ok(GetLawFirmRoleOptions()))
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
        result[LookupCategory.ContactType] = BuildLegacyLookupOptions(
            result.GetValueOrDefault(LookupCategory.ContactType, []),
            LookupCategory.ContactType,
            LegacyContactTypeOptions);
        result[LookupCategory.CaseStatus] = BuildLegacyLookupOptions(
            result.GetValueOrDefault(LookupCategory.CaseStatus, []),
            LookupCategory.CaseStatus,
            LegacyCaseStatusOptions);
        result[LookupCategory.AccidentType] = BuildLegacyLookupOptions(
            result.GetValueOrDefault(LookupCategory.AccidentType, []),
            LookupCategory.AccidentType,
            LegacyAccidentTypeOptions);
        result[LookupCategory.LienStatus] = BuildLegacyLookupOptions(
            result.GetValueOrDefault(LookupCategory.LienStatus, []),
            LookupCategory.LienStatus,
            LegacyLienStatusOptions);
        result[LookupCategory.DocumentCategory] = BuildLegacyDocumentLookupOptions(
            result.GetValueOrDefault(LookupCategory.DocumentCategory, []),
            LookupCategory.DocumentCategory);
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
        if (string.Equals(category, LookupCategory.CaseStatus, StringComparison.Ordinal))
            result = BuildLegacyLookupOptions(result, LookupCategory.CaseStatus, LegacyCaseStatusOptions);
        else if (string.Equals(category, LookupCategory.AccidentType, StringComparison.Ordinal))
            result = BuildLegacyLookupOptions(result, LookupCategory.AccidentType, LegacyAccidentTypeOptions);
        else if (string.Equals(category, LookupCategory.LienStatus, StringComparison.Ordinal))
            result = BuildLegacyLookupOptions(result, LookupCategory.LienStatus, LegacyLienStatusOptions);
        else if (string.Equals(category, LookupCategory.DocumentCategory, StringComparison.Ordinal))
            result = BuildLegacyDocumentLookupOptions(result, LookupCategory.DocumentCategory);
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
        LiensDbContext db,
        ICurrentRequestContext ctx,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var manualCosts = await db.ManualMedicalCodes
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.Code == code && m.Status == "A")
            .Select(m => new
            {
                code = m.Code,
                description = m.Description ?? string.Empty,
                facilityType = m.FacilityType,
                cost = m.Cost.ToString(),
                copay = m.Copay.ToString(),
                facilityTotal = m.FacilityTotal.ToString(),
                physicianTotal = m.PhysicianTotal.ToString(),
                total = m.Total.ToString(),
            })
            .ToListAsync(ct);

        if (manualCosts.Count > 0)
            return Results.Ok(new { isSuccess = true, message = "Retrieved from manual medical codes.", data = manualCosts });

        var medicareCosts = await GetMedicareProcedureCostsAsync(httpClientFactory, code, ct);
        if (medicareCosts.Count > 0)
        {
            var data = medicareCosts
                .Select(m => new
                {
                    code = string.IsNullOrWhiteSpace(m.Code) ? code : m.Code,
                    description = string.Empty,
                    facilityType = m.FacilityType,
                    cost = FormatDecimal(m.Cost),
                    copay = FormatDecimal(m.Copay),
                    facilityTotal = FormatDecimal(m.FacilityTotal),
                    physicianTotal = FormatDecimal(m.PhysicianTotal),
                    total = FormatDecimal(m.Total),
                })
                .ToList();

            return Results.Ok(new { isSuccess = true, message = "Retrieved from Medicare procedure price lookup.", data });
        }

        return Results.NotFound(new { isSuccess = false, message = "Unable to get procedure cost." });
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

    private static async Task<IResult> GetLegacyContactTypes(
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var result = await lookupService.GetByCategoryAsync(ctx.TenantId, LookupCategory.ContactType, ct);
        return Results.Ok(BuildLegacyLookupOptions(result, LookupCategory.ContactType, LegacyContactTypeOptions));
    }

    private static async Task<IResult> GetLegacyDocumentTypes(
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var result = await lookupService.GetByCategoryAsync(ctx.TenantId, LookupCategory.DocumentCategory, ct);
        return Results.Ok(BuildLegacyDocumentLookupOptions(result, LookupCategory.DocumentCategory));
    }

    private static async Task<IResult> GetLegacyCaseStatuses(
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var result = await lookupService.GetByCategoryAsync(ctx.TenantId, LookupCategory.CaseStatus, ct);
        return Results.Ok(BuildLegacyLookupOptions(result, LookupCategory.CaseStatus, LegacyCaseStatusOptions));
    }

    private static async Task<IResult> GetLegacyAccidentTypes(
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var result = await lookupService.GetByCategoryAsync(ctx.TenantId, LookupCategory.AccidentType, ct);
        return Results.Ok(BuildLegacyLookupOptions(result, LookupCategory.AccidentType, LegacyAccidentTypeOptions));
    }

    private static async Task<IResult> GetLegacyLienStatuses(
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var result = await lookupService.GetByCategoryAsync(ctx.TenantId, LookupCategory.LienStatus, ct);
        return Results.Ok(BuildLegacyLookupOptions(result, LookupCategory.LienStatus, LegacyLienStatusOptions));
    }

    private static List<LookupValueResponse> BuildLegacyLookupOptions(
        IReadOnlyList<LookupValueResponse> source,
        string category,
        IEnumerable<(string Code, string Name, string[] SourceCodes, int SortOrder)> options)
    {
        var byCode = source.ToDictionary(item => item.Code, StringComparer.Ordinal);

        return options
            .Select(option =>
            {
                var match = option.SourceCodes
                    .Select(code => byCode.TryGetValue(code, out var item) ? item : null)
                    .FirstOrDefault(item => item is not null);

                return new LookupValueResponse
                {
                    Id = match?.Id ?? Guid.Empty,
                    Category = match?.Category ?? category,
                    Code = option.Code,
                    Name = option.Name,
                    Description = match?.Description,
                    SortOrder = option.SortOrder,
                    IsActive = match?.IsActive ?? true,
                    IsSystem = match?.IsSystem ?? true,
                };
            })
            .ToList();
    }

    private static List<LookupValueResponse> BuildLegacyDocumentLookupOptions(
        IReadOnlyList<LookupValueResponse> source,
        string category)
    {
        var byCode = source.ToDictionary(item => item.Code, StringComparer.Ordinal);
        var byName = source.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);

        return LegacyDocumentTypeOptions
            .Select(option =>
            {
                var match = option.SourceCodes
                    .Select(code => byCode.TryGetValue(code, out var item) ? item : null)
                    .FirstOrDefault(item => item is not null);

                match ??= byCode.TryGetValue(option.Code, out var byCodeMatch) ? byCodeMatch : null;
                match ??= byName.TryGetValue(option.Name, out var byNameMatch) ? byNameMatch : null;

                return new LookupValueResponse
                {
                    Id = match?.Id ?? option.FallbackId,
                    Category = match?.Category ?? category,
                    Code = option.Code,
                    Name = option.Name,
                    Description = match?.Description,
                    SortOrder = option.SortOrder,
                    IsActive = match?.IsActive ?? true,
                    IsSystem = match?.IsSystem ?? true,
                };
            })
            .ToList();
    }

    private static async Task<IResult> GetLegacyProcedureCodes(
        ILookupValueService lookupService,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var tenantId = ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var lookupCodes = await lookupService.GetByCategoryAsync(tenantId, LookupCategory.ProcedureCode, ct);
        var data = lookupCodes
            .Select(l => new
            {
                code = l.Code,
                description = l.Description ?? l.Name,
            })
            .ToList();

        var existingCodes = new HashSet<string>(data.Select(item => item.code), StringComparer.OrdinalIgnoreCase);
        var medicareCodes = await GetMedicareProcedureCodesAsync(httpClientFactory, ct);
        foreach (var medicareCode in medicareCodes
            .Where(item => !string.IsNullOrWhiteSpace(item.Code))
            .OrderByDescending(item => item.Frequency)
            .ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase))
        {
            if (existingCodes.Add(medicareCode.Code))
            {
                data.Add(new
                {
                    code = medicareCode.Code,
                    description = string.IsNullOrWhiteSpace(medicareCode.Description)
                        ? medicareCode.Code
                        : medicareCode.Description,
                });
            }
        }

        var manualCodes = await db.ManualMedicalCodes
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.Status == "A")
            .OrderBy(m => m.Description)
            .ThenBy(m => m.Code)
            .Select(m => new
            {
                code = m.Code,
                description = $"{m.Description ?? string.Empty} ({m.Code})",
            })
            .ToListAsync(ct);

        foreach (var manualCode in manualCodes)
        {
            if (existingCodes.Add(manualCode.code))
            {
                data.Add(manualCode);
            }
        }

        return Results.Ok(new { isSuccess = true, message = string.Empty, data });
    }

    private static async Task<IReadOnlyList<MedicareProcedureCode>> GetMedicareProcedureCodesAsync(
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        try
        {
            using var response = await SendMedicareRequestAsync(httpClientFactory, "codes", ct);
            if (!response.IsSuccessStatusCode)
                return [];

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<List<MedicareProcedureCode>>(stream, MedicareJsonOptions, ct)
                ?? [];
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static async Task<IReadOnlyList<MedicareProcedureCost>> GetMedicareProcedureCostsAsync(
        IHttpClientFactory httpClientFactory,
        string code,
        CancellationToken ct)
    {
        try
        {
            using var response = await SendMedicareRequestAsync(httpClientFactory, $"costs/{Uri.EscapeDataString(code)}", ct);
            if (!response.IsSuccessStatusCode)
                return [];

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<List<MedicareProcedureCost>>(stream, MedicareJsonOptions, ct)
                ?? [];
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static async Task<HttpResponseMessage> SendMedicareRequestAsync(
        IHttpClientFactory httpClientFactory,
        string path,
        CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(MedicareProcedureLookupClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("apiKey", MedicareProcedureLookupApiKey);
        request.Headers.TryAddWithoutValidation("amaLicense", MedicareProcedureLookupAmaLicense);

        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static string FormatDecimal(decimal? value)
        => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;

    private sealed record MedicareProcedureCode(
        string Code,
        string Description,
        int Frequency);

    private sealed record MedicareProcedureCost(
        string? Code,
        string FacilityType,
        decimal Cost,
        decimal Copay,
        decimal? FacilityTotal,
        decimal? PhysicianTotal,
        decimal Total);

    private static async Task<IResult> LegacyGetContactsByType(
        IContactService contactService,
        ICurrentRequestContext ctx,
        string? contactType,
        CancellationToken ct)
    {
        var tenantId = ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var result = await contactService.GetAllByTypeAsync(tenantId, contactType, isActive: true, ct);

        if (string.Equals(contactType, ContactType.LawFirm, StringComparison.Ordinal))
        {
            result = result
                .Where(contact => string.IsNullOrWhiteSpace(contact.ContactSubtype))
                .ToList();
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> LegacyGetFundingCompanies(
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var lienHolders = await contactService.GetAllByTypeAsync(tenantId, ContactType.LienHolder, isActive: true, ct);
        var fundingCompanies = await contactService.GetAllByTypeAsync(tenantId, ContactType.FundingCompany, isActive: true, ct);

        var result = lienHolders
            .Concat(fundingCompanies)
            .DistinctBy(item => item.Id)
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

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
