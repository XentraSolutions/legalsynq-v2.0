using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;
using System.Globalization;

namespace Liens.Api.Endpoints;

public static class LienEndpoints
{
    private sealed class LegacyLiensMedicalListing
    {
        public List<LegacyLiensMedicalListItem> medicalList { get; init; } = [];
        public List<LegacyLiensMedicalFacilityListItem> facilityList { get; init; } = [];
        public List<LegacyLiensMedicalCodeListItem> codeList { get; init; } = [];
        public List<LegacyLiensMedicalDocumentListItem> documentList { get; init; } = [];
    }

    private sealed class LegacyLiensMedicalListItem
    {
        public string id { get; init; } = string.Empty;
        public string caseId { get; init; } = string.Empty;
        public string status { get; init; } = string.Empty;
        public string purchaseDate { get; init; } = string.Empty;
        public string initialServiceDate { get; init; } = string.Empty;
        public string endServiceDate { get; init; } = string.Empty;
        public string note { get; init; } = string.Empty;
        public string created { get; init; } = string.Empty;
        public string createdBy { get; init; } = string.Empty;
        public string updated { get; init; } = string.Empty;
        public string updatedBy { get; init; } = string.Empty;
        public string fundingCompanyId { get; init; } = string.Empty;
        public string fundingCompany { get; init; } = string.Empty;
        public string isBulk { get; init; } = string.Empty;
        public string isServicing { get; init; } = string.Empty;
    }

    private sealed class LegacyLiensMedicalFacilityListItem
    {
        public string id { get; init; } = string.Empty;
        public string liensId { get; init; } = string.Empty;
        public string facilityId { get; init; } = string.Empty;
        public string facilityContactId { get; init; } = string.Empty;
        public string email { get; init; } = string.Empty;
        public string phone { get; init; } = string.Empty;
        public string medicalProviderId { get; init; } = string.Empty;
        public string created { get; init; } = string.Empty;
        public string createdBy { get; init; } = string.Empty;
        public string updated { get; init; } = string.Empty;
        public string updatedBy { get; init; } = string.Empty;
    }

    private sealed class LegacyLiensMedicalCodeListItem
    {
        public string id { get; init; } = string.Empty;
        public string liensId { get; init; } = string.Empty;
        public string code { get; init; } = string.Empty;
        public string medicareCost { get; init; } = string.Empty;
        public string billingAmount { get; init; } = string.Empty;
        public string purchaseAmount { get; init; } = string.Empty;
        public string created { get; init; } = string.Empty;
        public string createdBy { get; init; } = string.Empty;
        public string updated { get; init; } = string.Empty;
        public string updatedBy { get; init; } = string.Empty;
    }

    private sealed class LegacyLiensMedicalDocumentListItem
    {
        public string id { get; init; } = string.Empty;
        public string liensId { get; init; } = string.Empty;
        public string filename { get; init; } = string.Empty;
        public string typeId { get; init; } = string.Empty;
        public string url { get; init; } = string.Empty;
        public string status { get; init; } = string.Empty;
    }

    private sealed class LegacyReassignMedicalProviderRequest
    {
        public string? liensId { get; init; }
        public string? medicalProvider { get; init; }
    }

    private sealed class LegacyReassignFacilityRequest
    {
        public string? liensId { get; init; }
        public string? facility { get; init; }
    }

    private sealed class LegacyReassignFacilityContactPersonRequest
    {
        public string? liensId { get; init; }
        public string? facilityContactPerson { get; init; }
    }

    private sealed class LegacyReassignFundingCompanyRequest
    {
        public string? liensId { get; init; }
        public string? fundingCompany { get; init; }
    }

    public static void MapLienEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/liens")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/", ListLiens)
            .RequirePermission(LiensPermissions.LienRead);

        group.MapGet("/{id:guid}", GetLienById)
            .RequirePermission(LiensPermissions.LienRead);

        group.MapGet("/by-number/{lienNumber}", GetLienByNumber)
            .RequirePermission(LiensPermissions.LienRead);

        // Legacy transfer from CaseEndpoints: full listing behavior from GetLeinsMedicalFullListing.
        group.MapPost("/full-listing", GetLeinsMedicalFullListingLegacy)
            .RequirePermission(LiensPermissions.LienRead);

        // Legacy transfer from CaseEndpoints: case-specific full listing behavior.
        group.MapPost("/full-listing/{caseId:guid}", GetLeinsMedicalFullListingByCaseLegacy)
            .RequirePermission(LiensPermissions.LienRead);

        // Legacy compatibility route from previous service: POST /case/liens/details/{caseId}
        // under the liens base path becomes POST /api/liens/liens/details/{lienId}.
        group.MapPost("/details/{lienId:guid}", GetLeinsMedicalListingLegacy)
            .RequirePermission(LiensPermissions.LienRead);

        // Legacy compatibility route from previous service: DELETE /case/liens/delete-medicalcode/{id}
        // under the liens base path becomes DELETE /api/liens/liens/delete-medicalcode/{id}.
        group.MapDelete("/delete-medicalcode/{id:guid}", DeleteMedicalCodeLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy compatibility route from previous service: DELETE /case/liens/delete-medicaldocument/{id}
        // under the liens base path becomes DELETE /api/liens/liens/delete-medicaldocument/{id}.
        group.MapDelete("/delete-medicaldocument/{id:guid}", DeleteMedicalDocumentLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy compatibility route from previous service: DELETE /case/delete-casedocument/{id}
        // under the liens base path becomes DELETE /api/liens/liens/delete-casedocument/{id}.
        group.MapDelete("/delete-casedocument/{id:guid}", DeleteCaseDocumentLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy transfer from CaseEndpoints: POST /case/liens/reassign/medical-provider.
        group.MapPost("/reassign/medical-provider", ReassignMedicalProviderLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy transfer from CaseEndpoints: POST /case/liens/reassign/facility.
        group.MapPost("/reassign/facility", ReassignFacilityLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy migration from CaseController: POST /case/liens/reassign/contact-person.
        group.MapPost("/reassign/contact-person", ReassignFacilityContactPersonLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy migration from CaseController: POST /case/liens/reassign/funding-company.
        group.MapPost("/reassign/funding-company", ReassignFundingCompanyLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy compatibility route from previous service: DELETE /case/liens/delete/{liensId}
        group.MapDelete("/delete/{liensId:guid}", DeleteLiensLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        group.MapPost("/", CreateLien)
            .RequirePermission(LiensPermissions.LienCreate);

        group.MapPut("/{id:guid}", UpdateLien)
            .RequirePermission(LiensPermissions.LienUpdate);
    }

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

    private static async Task<IResult> ListLiens(
        ILienService lienService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        string? search = null,
        string? status = null,
        string? lienType = null,
        Guid? caseId = null,
        Guid? facilityId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await lienService.SearchAsync(
            tenantId, search, status, lienType, caseId, facilityId, page, pageSize, ct);
        var enriched = await EnrichLienResponsesAsync(result.Items, tenantId, servicingItemService, ct);
        return Results.Ok(new PaginatedResult<LienResponse>
        {
            Items = enriched,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        });
    }

    private static async Task<IResult> GetLienById(
        Guid id,
        ILienService lienService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await lienService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Lien '{id}' not found." } })
            : Results.Ok((await EnrichLienResponsesAsync([result], tenantId, servicingItemService, ct)).Single());
    }

    private static async Task<IResult> GetLienByNumber(
        string lienNumber,
        ILienService lienService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await lienService.GetByLienNumberAsync(tenantId, lienNumber, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Lien with number '{lienNumber}' not found." } })
            : Results.Ok((await EnrichLienResponsesAsync([result], tenantId, servicingItemService, ct)).Single());
    }

    private static async Task<List<LienResponse>> EnrichLienResponsesAsync(
        List<LienResponse> liens,
        Guid tenantId,
        IServicingItemService servicingItemService,
        CancellationToken ct)
    {
        var enriched = new List<LienResponse>(liens.Count);

        foreach (var lien in liens)
        {
            var codeResults = await servicingItemService.SearchAsync(
                tenantId,
                search: "LegacyMedicalCode",
                status: null,
                priority: null,
                assignedTo: null,
                caseId: null,
                lienId: lien.Id,
                page: 1,
                pageSize: 500,
                ct);

            var totalPurchase = 0m;
            var totalBilling = 0m;

            foreach (var item in codeResults.Items.Where(i =>
                         string.Equals(i.TaskType, "LegacyMedicalCode", StringComparison.Ordinal)))
            {
                var codeFields = ParseLegacyNoteFields(item.Notes);
                if (decimal.TryParse(codeFields.GetValueOrDefault("purchaseAmount", string.Empty), NumberStyles.Any, CultureInfo.InvariantCulture, out var purchase))
                    totalPurchase += purchase;
                if (decimal.TryParse(codeFields.GetValueOrDefault("billingAmount", string.Empty), NumberStyles.Any, CultureInfo.InvariantCulture, out var billing))
                    totalBilling += billing;
            }

            enriched.Add(new LienResponse
            {
                Id = lien.Id,
                LienNumber = lien.LienNumber,
                ExternalReference = lien.ExternalReference,
                LienType = lien.LienType,
                Status = lien.Status,
                CaseId = lien.CaseId,
                FacilityId = lien.FacilityId,
                OriginalAmount = lien.OriginalAmount,
                CurrentBalance = lien.CurrentBalance,
                OfferPrice = lien.OfferPrice,
                PurchasePrice = lien.PurchasePrice,
                PayoffAmount = lien.PayoffAmount,
                Jurisdiction = lien.Jurisdiction,
                IsConfidential = lien.IsConfidential,
                SubjectFirstName = lien.SubjectFirstName,
                SubjectLastName = lien.SubjectLastName,
                SubjectDisplayName = lien.SubjectDisplayName,
                OrgId = lien.OrgId,
                SellingOrgId = lien.SellingOrgId,
                BuyingOrgId = lien.BuyingOrgId,
                HoldingOrgId = lien.HoldingOrgId,
                IncidentDate = lien.IncidentDate,
                PurchaseDate = lien.IncidentDate.HasValue ? FormatLegacyDate(lien.IncidentDate) : lien.PurchaseDate,
                InitialServiceDate = lien.InitialServiceDate,
                EndServiceDate = lien.EndServiceDate,
                TotalPurchase = totalPurchase,
                TotalBilling = totalBilling,
                IsBulk = lien.IsBulk,
                IsServicing = lien.IsServicing,
                Description = lien.Description,
                OpenedAtUtc = lien.OpenedAtUtc,
                ClosedAtUtc = lien.ClosedAtUtc,
                CreatedAtUtc = lien.CreatedAtUtc,
                UpdatedAtUtc = lien.UpdatedAtUtc,
            });
        }

        return enriched;
    }

    private static async Task<IResult> CreateLien(
        CreateLienRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await lienService.CreateAsync(tenantId, orgId, userId, request, ct);
        return Results.Created($"/api/liens/liens/{result.Id}", result);
    }

    private static async Task<IResult> GetLeinsMedicalFullListingLegacy(
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        try
        {
            var data = await GetLegacyLiensListingAsync(lienService, tenantId, null, ct);
            if (data.Count == 0)
            {
                return Results.NotFound(new
                {
                    isSuccess = false,
                    message = "No Liens Found.",
                });
            }

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Liens List.",
                data,
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = $"Error: retrieving data. {ex.Message}",
            });
        }
    }

    private static async Task<IResult> GetLeinsMedicalFullListingByCaseLegacy(
        Guid caseId,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        try
        {
            var data = await GetLegacyLiensListingAsync(lienService, tenantId, caseId, ct);
            if (data.Count == 0)
            {
                return Results.NotFound(new
                {
                    isSuccess = false,
                    message = "No Liens Found.",
                });
            }

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Liens List.",
                data,
            });
        }
        catch (Exception)
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = "Error: retrieving data.",
            });
        }
    }

    private static async Task<IResult> GetLeinsMedicalListingLegacy(
        Guid lienId,
        ILienService lienService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        try
        {
            var data = new LegacyLiensMedicalListing();
            var lien = await lienService.GetByIdAsync(tenantId, lienId, ct);

            if (lien is not null)
            {
                data.medicalList.Add(new LegacyLiensMedicalListItem
                {
                    id = lien.Id.ToString(),
                    caseId = lien.CaseId?.ToString() ?? string.Empty,
                    status = lien.Status,
                    purchaseDate = FormatLegacyDate(lien.IncidentDate),
                    initialServiceDate = FormatLegacyDate(lien.InitialServiceDate),
                    endServiceDate = FormatLegacyDate(lien.EndServiceDate),
                    note = lien.Description ?? string.Empty,
                    created = FormatLegacyTimestamp(lien.CreatedAtUtc),
                    createdBy = string.Empty,
                    updated = FormatLegacyTimestamp(lien.UpdatedAtUtc),
                    updatedBy = string.Empty,
                    fundingCompanyId = lien.ExternalReference ?? string.Empty,
                    fundingCompany = string.Empty,
                    isBulk = lien.IsBulk ?? string.Empty,
                    isServicing = lien.IsServicing ?? string.Empty,
                });

                if (lien.FacilityId.HasValue)
                {
                    data.facilityList.Add(new LegacyLiensMedicalFacilityListItem
                    {
                        id = string.Empty,
                        liensId = lien.Id.ToString(),
                        facilityId = lien.FacilityId.Value.ToString(),
                        facilityContactId = string.Empty,
                        email = string.Empty,
                        phone = string.Empty,
                        medicalProviderId = string.Empty,
                        created = FormatLegacyTimestamp(lien.CreatedAtUtc),
                        createdBy = string.Empty,
                        updated = FormatLegacyTimestamp(lien.UpdatedAtUtc),
                        updatedBy = string.Empty,
                    });
                }
            }

            var codeResults = await servicingItemService.SearchAsync(
                tenantId,
                search: "LegacyMedicalCode",
                status: null,
                priority: null,
                assignedTo: null,
                caseId: null,
                lienId: lienId,
                page: 1,
                pageSize: 500,
                ct);

            foreach (var item in codeResults.Items.Where(i =>
                string.Equals(i.TaskType, "LegacyMedicalCode", StringComparison.Ordinal) &&
                i.LienId == lienId))
            {
                var fields = ParseLegacyNoteFields(item.Notes);
                data.codeList.Add(new LegacyLiensMedicalCodeListItem
                {
                    id = item.Id.ToString(),
                    liensId = item.LienId?.ToString() ?? string.Empty,
                    code = fields.GetValueOrDefault("code", string.Empty),
                    medicareCost = fields.GetValueOrDefault("medicareCost", string.Empty),
                    billingAmount = fields.GetValueOrDefault("billingAmount", string.Empty),
                    purchaseAmount = fields.GetValueOrDefault("purchaseAmount", string.Empty),
                    created = FormatLegacyTimestamp(item.CreatedAtUtc),
                    createdBy = string.Empty,
                    updated = FormatLegacyTimestamp(item.UpdatedAtUtc),
                    updatedBy = string.Empty,
                });
            }

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Liens details.",
                data,
            });
        }
        catch (Exception)
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = "Error: retrieving data.",
            });
        }
    }

    private static async Task<IResult> DeleteMedicalCodeLegacy(
        Guid id,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        var existing = await servicingItemService.GetByIdAsync(tenantId, id, ct);
        if (existing is null || !string.Equals(existing.TaskType, "LegacyMedicalCode", StringComparison.Ordinal))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found to delete.",
            });
        }

        try
        {
            await servicingItemService.DeleteAsync(tenantId, id, userId, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully delete medical code record.",
            });
        }
        catch (Exception ex)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = ex.Message,
            });
        }
    }

    private static async Task<IResult> DeleteMedicalDocumentLegacy(
        Guid id,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        var existing = await servicingItemService.GetByIdAsync(tenantId, id, ct);
        if (existing is null || !string.Equals(existing.TaskType, "LegacyMedicalDocument", StringComparison.Ordinal))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: Unable to delete Medical Document.",
            });
        }

        try
        {
            await servicingItemService.DeleteAsync(tenantId, id, userId, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully deleted Medical Document.",
            });
        }
        catch (Exception ex)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = ex.Message,
            });
        }
    }

    private static async Task<IResult> DeleteCaseDocumentLegacy(
        Guid id,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        var existing = await servicingItemService.GetByIdAsync(tenantId, id, ct);
        if (existing is null || !string.Equals(existing.TaskType, "LegacyCaseDocument", StringComparison.Ordinal))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: Unable to delete Case Document.",
            });
        }

        try
        {
            await servicingItemService.DeleteAsync(tenantId, id, userId, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully deleted case document.",
            });
        }
        catch (Exception ex)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = ex.Message,
            });
        }
    }

    private static async Task<IResult> ReassignMedicalProviderLegacy(
        LegacyReassignMedicalProviderRequest request,
        ILienService lienService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.liensId, out var lienId) || string.IsNullOrWhiteSpace(request.medicalProvider))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }

        var lien = await lienService.GetByIdAsync(tenantId, lienId, ct);
        if (lien is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }

        try
        {
            var infoResult = await servicingItemService.SearchAsync(
                tenantId,
                search: "LegacyMedicalFacilityInfo",
                status: null,
                priority: null,
                assignedTo: null,
                caseId: null,
                lienId: lienId,
                page: 1,
                pageSize: 50,
                ct);

            var existing = infoResult.Items.FirstOrDefault(i =>
                string.Equals(i.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal) &&
                i.LienId == lienId);

            if (existing is null)
            {
                var create = new CreateServicingItemRequest
                {
                    TaskNumber = $"LMFI-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    TaskType = "LegacyMedicalFacilityInfo",
                    Description = "Legacy medical facility information",
                    AssignedTo = "system",
                    CaseId = lien.CaseId,
                    LienId = lienId,
                    Notes = $"medicalProviderId={request.medicalProvider.Trim()}",
                };

                await servicingItemService.CreateAsync(tenantId, orgId, userId, create, ct);
            }
            else
            {
                var fields = ParseLegacyNoteFields(existing.Notes);
                fields["medicalProviderId"] = request.medicalProvider.Trim();

                var update = new UpdateServicingItemRequest
                {
                    TaskType = existing.TaskType,
                    Description = existing.Description,
                    AssignedTo = string.IsNullOrWhiteSpace(existing.AssignedTo) ? "system" : existing.AssignedTo,
                    AssignedToUserId = existing.AssignedToUserId,
                    Priority = existing.Priority,
                    Status = existing.Status,
                    CaseId = existing.CaseId,
                    LienId = existing.LienId,
                    DueDate = existing.DueDate,
                    Notes = SerializeLegacyNoteFields(fields),
                    Resolution = existing.Resolution,
                };

                await servicingItemService.UpdateAsync(tenantId, existing.Id, userId, update, ct);
            }

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully re-assigned liens to new medical provider.",
            });
        }
        catch
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }
    }

    private static async Task<IResult> ReassignFacilityLegacy(
        LegacyReassignFacilityRequest request,
        ILienService lienService,
        IContactService contactService,
        IFacilityService facilityService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.liensId, out var lienId) ||
            !Guid.TryParse(request.facility, out var facilityId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }

        var lien = await lienService.GetByIdAsync(tenantId, lienId, ct);
        if (lien is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }

        try
        {
            var mapped = new UpdateLienRequest
            {
                ExternalReference = lien.ExternalReference,
                LienType = lien.LienType,
                CaseId = lien.CaseId,
                FacilityId = facilityId,
                OriginalAmount = lien.OriginalAmount,
                Jurisdiction = lien.Jurisdiction,
                IsConfidential = lien.IsConfidential,
                SubjectFirstName = lien.SubjectFirstName,
                SubjectLastName = lien.SubjectLastName,
                IncidentDate = lien.IncidentDate,
                Description = lien.Description,
            };

            await lienService.UpdateAsync(tenantId, lienId, userId, mapped, ct);
            var facilityName = await ResolveLegacyFacilityNameAsync(
                tenantId,
                facilityId,
                contactService,
                facilityService,
                ct);

            var infoResult = await servicingItemService.SearchAsync(
                tenantId,
                search: "LegacyMedicalFacilityInfo",
                status: null,
                priority: null,
                assignedTo: null,
                caseId: null,
                lienId: lienId,
                page: 1,
                pageSize: 50,
                ct);

            var existing = infoResult.Items.FirstOrDefault(i =>
                string.Equals(i.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal) &&
                i.LienId == lienId);

            if (existing is null)
            {
                var fields = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["facilityId"] = facilityId.ToString(),
                };

                if (!string.IsNullOrWhiteSpace(facilityName))
                    fields["facilityName"] = facilityName;

                var create = new CreateServicingItemRequest
                {
                    TaskNumber = $"LMFI-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    TaskType = "LegacyMedicalFacilityInfo",
                    Description = "Legacy medical facility information",
                    AssignedTo = "system",
                    CaseId = lien.CaseId,
                    LienId = lienId,
                    Notes = SerializeLegacyNoteFields(fields),
                };

                await servicingItemService.CreateAsync(tenantId, orgId, userId, create, ct);
            }
            else
            {
                var fields = ParseLegacyNoteFields(existing.Notes);
                fields["facilityId"] = facilityId.ToString();
                if (!string.IsNullOrWhiteSpace(facilityName))
                    fields["facilityName"] = facilityName;

                var update = new UpdateServicingItemRequest
                {
                    TaskType = existing.TaskType,
                    Description = existing.Description,
                    AssignedTo = string.IsNullOrWhiteSpace(existing.AssignedTo) ? "system" : existing.AssignedTo,
                    AssignedToUserId = existing.AssignedToUserId,
                    Priority = existing.Priority,
                    Status = existing.Status,
                    CaseId = existing.CaseId,
                    LienId = existing.LienId,
                    DueDate = existing.DueDate,
                    Notes = SerializeLegacyNoteFields(fields),
                    Resolution = existing.Resolution,
                };

                await servicingItemService.UpdateAsync(tenantId, existing.Id, userId, update, ct);
            }

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully re-assigned liens to new facility.",
            });
        }
        catch
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }
    }

    private static async Task<IResult> ReassignFacilityContactPersonLegacy(
        LegacyReassignFacilityContactPersonRequest request,
        ILienService lienService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.liensId, out var lienId) ||
            !Guid.TryParse(request.facilityContactPerson, out var facilityContactId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }

        var lien = await lienService.GetByIdAsync(tenantId, lienId, ct);
        if (lien is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }

        try
        {
            var infoResult = await servicingItemService.SearchAsync(
                tenantId,
                search: "LegacyMedicalFacilityInfo",
                status: null,
                priority: null,
                assignedTo: null,
                caseId: null,
                lienId: lienId,
                page: 1,
                pageSize: 50,
                ct);

            var existing = infoResult.Items.FirstOrDefault(i =>
                string.Equals(i.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal) &&
                i.LienId == lienId);

            if (existing is null)
            {
                var create = new CreateServicingItemRequest
                {
                    TaskNumber = $"LMFI-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    TaskType = "LegacyMedicalFacilityInfo",
                    Description = "Legacy medical facility information",
                    AssignedTo = "system",
                    CaseId = lien.CaseId,
                    LienId = lienId,
                    Notes = $"facilityContactId={facilityContactId}",
                };

                await servicingItemService.CreateAsync(tenantId, orgId, userId, create, ct);
            }
            else
            {
                var fields = ParseLegacyNoteFields(existing.Notes);
                fields["facilityContactId"] = facilityContactId.ToString();

                var update = new UpdateServicingItemRequest
                {
                    TaskType = existing.TaskType,
                    Description = existing.Description,
                    AssignedTo = string.IsNullOrWhiteSpace(existing.AssignedTo) ? "system" : existing.AssignedTo,
                    AssignedToUserId = existing.AssignedToUserId,
                    Priority = existing.Priority,
                    Status = existing.Status,
                    CaseId = existing.CaseId,
                    LienId = existing.LienId,
                    DueDate = existing.DueDate,
                    Notes = SerializeLegacyNoteFields(fields),
                    Resolution = existing.Resolution,
                };

                await servicingItemService.UpdateAsync(tenantId, existing.Id, userId, update, ct);
            }

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully re-assigned liens to new facility contact person.",
            });
        }
        catch
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }
    }

    private static async Task<IResult> ReassignFundingCompanyLegacy(
        LegacyReassignFundingCompanyRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.liensId, out var lienId) || string.IsNullOrWhiteSpace(request.fundingCompany))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }

        var lien = await lienService.GetByIdAsync(tenantId, lienId, ct);
        if (lien is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }

        try
        {
            var mapped = new UpdateLienRequest
            {
                ExternalReference = request.fundingCompany.Trim(),
                LienType = lien.LienType,
                CaseId = lien.CaseId,
                FacilityId = lien.FacilityId,
                OriginalAmount = lien.OriginalAmount,
                Jurisdiction = lien.Jurisdiction,
                IsConfidential = lien.IsConfidential,
                SubjectFirstName = lien.SubjectFirstName,
                SubjectLastName = lien.SubjectLastName,
                IncidentDate = lien.IncidentDate,
                Description = lien.Description,
            };

            await lienService.UpdateAsync(tenantId, lienId, userId, mapped, ct);

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully re-assigned liens to new funding company.",
            });
        }
        catch
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned liens.",
            });
        }
    }

    private static async Task<IResult> DeleteLiensLegacy(
        Guid liensId,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);

        var existing = await lienService.GetByIdAsync(tenantId, liensId, ct);
        if (existing is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found to delete.",
            });
        }

        try
        {
            await lienService.DeleteAsync(tenantId, liensId, userId, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully Deleted Lien.",
            });
        }
        catch (Exception ex)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = ex.Message,
            });
        }
    }

    private static async Task<List<LienResponse>> GetLegacyLiensListingAsync(
        ILienService lienService,
        Guid tenantId,
        Guid? caseId,
        CancellationToken ct)
    {
        const int pageSize = 200;
        var page = 1;
        var data = new List<LienResponse>();

        while (true)
        {
            var result = await lienService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                lienType: null,
                caseId: caseId,
                facilityId: null,
                page: page,
                pageSize: pageSize,
                ct);

            if (result.Items.Count == 0)
                break;

            data.AddRange(result.Items);

            if (data.Count >= result.TotalCount)
                break;

            page++;
        }

        return data;
    }

    private static async Task<string?> ResolveLegacyFacilityNameAsync(
        Guid tenantId,
        Guid requestedFacilityId,
        IContactService contactService,
        IFacilityService facilityService,
        CancellationToken ct)
    {
        var facilityContact = await contactService.GetByIdAsync(tenantId, requestedFacilityId, ct);
        if (facilityContact is not null && IsStandaloneFacilityContact(facilityContact))
            return ResolveFacilityDisplayName(facilityContact);

        var facility = await facilityService.GetByIdAsync(tenantId, requestedFacilityId, ct);
        if (facility is not null && !string.IsNullOrWhiteSpace(facility.Name))
            return facility.Name.Trim();

        if (facilityContact?.FacilityId is Guid linkedFacilityId)
        {
            var linkedFacility = await facilityService.GetByIdAsync(tenantId, linkedFacilityId, ct);
            if (linkedFacility is not null && !string.IsNullOrWhiteSpace(linkedFacility.Name))
                return linkedFacility.Name.Trim();
        }

        return null;
    }

    private static bool IsStandaloneFacilityContact(ContactResponse contact) =>
        (string.Equals(contact.ContactType, ContactType.Facility, StringComparison.Ordinal) ||
         string.Equals(contact.ContactType, ContactType.MedicalFacility, StringComparison.Ordinal)) &&
        string.IsNullOrWhiteSpace(contact.ContactSubtype);

    private static string ResolveFacilityDisplayName(ContactResponse contact)
        => string.IsNullOrWhiteSpace(contact.Organization)
            ? contact.DisplayName
            : contact.Organization.Trim();

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        foreach (var segment in notes.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq > 0)
            {
                var key = segment[..eq].Trim();
                var value = segment[(eq + 1)..].Trim();
                result[key] = value;
            }
        }

        return result;
    }

    private static string SerializeLegacyNoteFields(Dictionary<string, string> fields)
    {
        if (fields.Count == 0)
            return string.Empty;

        return string.Join("; ", fields.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string FormatLegacyDate(DateOnly? value)
        => value?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatLegacyTimestamp(DateTime value)
        => value.ToString("MM/dd/yyyy hh:mm tt", CultureInfo.InvariantCulture);

    private static async Task<IResult> UpdateLien(
        Guid id,
        UpdateLienRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await lienService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }
}
