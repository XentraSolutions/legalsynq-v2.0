using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;
using System.Globalization;

namespace Liens.Api.Endpoints;

public static class CaseEndpoints
{
    private sealed class LegacyCreateCaseRequest
    {
        public string? code { get; init; }
        public string? firstname { get; init; }
        public string? lastname { get; init; }
        public string? dob { get; init; }
        public string? address { get; init; }
        public string? city { get; init; }
        public string? state { get; init; }
        public string? zipcode { get; init; }
        public string? dateOfLoss { get; init; }
        public string? note { get; init; }
        public string? externalCaseId { get; init; }
    }

    private sealed class LegacyUpdateCaseRequest
    {
        public string? firstname { get; init; }
        public string? lastname { get; init; }
        public string? dob { get; init; }
        public string? address { get; init; }
        public string? city { get; init; }
        public string? state { get; init; }
        public string? zipcode { get; init; }
        public string? dateOfLoss { get; init; }
        public string? note { get; init; }
        public string? externalCaseId { get; init; }
    }

    private sealed class LegacyCaseV3FilterRequest
    {
        public int page { get; init; } = 1;
        public int limit { get; init; } = 20;
        public string? lawFirmId { get; init; }
        public string? accidentTypeId { get; init; }
        public string? statusId { get; init; }
        public string? caseManagerId { get; init; }
        public string? keyword { get; init; }
        public string? sortBy { get; init; }
        public string? sortDirection { get; init; }
    }

    private sealed class LegacyLiensMedicalInformationFacilityRequest
    {
        public string? id { get; init; }
        public string? liensId { get; init; }
        public string? facilityId { get; init; }
        public string? facilityContactId { get; init; }
        public string? email { get; init; }
        public string? phone { get; init; }
        public string? medicalProviderId { get; init; }
    }

    private sealed class LegacyLiensMedicalRequest
    {
        public string? id { get; init; }
        public string? caseId { get; init; }
        public string? status { get; init; }
        public string? purchaseDate { get; init; }
        public string? initialServiceDate { get; init; }
        public string? endServiceDate { get; init; }
        public string? note { get; init; }
        public string? isBulk { get; init; }
        public string? isServicing { get; init; }
        public string? fundingCompanyId { get; init; }
    }

    private sealed class LegacyLiensMedicalCodeRequest
    {
        public string? id { get; init; }
        public string? liensId { get; init; }
        public string? code { get; init; }
        public string? medicareCost { get; init; }
        public string? billingAmount { get; init; }
        public string? purchaseAmount { get; init; }
        public string? payee { get; init; }
        public string? outboundCheckNumber { get; init; }
    }

    private sealed class LegacyLiensMedicalCodeResponse
    {
        public string id { get; init; } = string.Empty;
        public string liensId { get; init; } = string.Empty;
        public string code { get; init; } = string.Empty;
        public string medicareCost { get; init; } = string.Empty;
        public string billingAmount { get; init; } = string.Empty;
        public string purchaseAmount { get; init; } = string.Empty;
        public string payee { get; init; } = string.Empty;
        public string outboundCheckNumber { get; init; } = string.Empty;
        public string created { get; init; } = string.Empty;
        public string createdBy { get; init; } = string.Empty;
        public string updated { get; init; } = string.Empty;
        public string updatedBy { get; init; } = string.Empty;
    }

    private sealed class LegacyPayeeOutboundRequest
    {
        public string? id { get; init; }
        public string? liensId { get; init; }
        public string? payee { get; init; }
        public string? outboundCheckNumber { get; init; }
    }

    private sealed class LegacyCaseManagerRequest
    {
        public string? firstname { get; init; }
        public string? lastname { get; init; }
        public string? email { get; init; }
        public string? phone { get; init; }
        public string? lawfirmId { get; init; }
        public string? roleId { get; init; }
    }

    private sealed class LegacyCaseManagerUpdateRequest
    {
        public string? id { get; init; }
        public string? firstname { get; init; }
        public string? lastname { get; init; }
        public string? email { get; init; }
        public string? phone { get; init; }
        public string? lawfirmId { get; init; }
        public string? roleId { get; init; }
    }

    private sealed class LegacyPayeeOutboundResponse
    {
        public string id { get; init; } = string.Empty;
        public string liensId { get; init; } = string.Empty;
        public string payee { get; init; } = string.Empty;
        public string outboundCheckNumber { get; init; } = string.Empty;
    }

    private sealed class LegacyLiensMedicalInformationFacilityResponse
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

    private sealed class LegacyLiensMedicalResponse
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

    private sealed class LegacyCaseInfoV2Response
    {
        public string caseId { get; init; } = string.Empty;
        public string caseCode { get; init; } = string.Empty;
        public string firstname { get; init; } = string.Empty;
        public string lastname { get; init; } = string.Empty;
        public string dateOfBirth { get; init; } = string.Empty;
        public string address { get; init; } = string.Empty;
        public string city { get; init; } = string.Empty;
        public string state { get; init; } = string.Empty;
        public string zipcode { get; init; } = string.Empty;
        public string isServicing { get; init; } = string.Empty;
        public string isUccFiled { get; init; } = string.Empty;
        public string isBulk { get; init; } = string.Empty;
        public string accidentType { get; init; } = string.Empty;
        public string accidentState { get; init; } = string.Empty;
        public string dateOfLoss { get; init; } = string.Empty;
        public string lawFirm { get; init; } = string.Empty;
        public string caseManager { get; init; } = string.Empty;
        public string note { get; init; } = string.Empty;
        public string created { get; init; } = string.Empty;
        public string createBy { get; init; } = string.Empty;
        public string updated { get; init; } = string.Empty;
        public string updateBy { get; init; } = string.Empty;
        public string status { get; init; } = string.Empty;
        public string currentStatus { get; init; } = string.Empty;
        public string currentMedicalStatus { get; init; } = string.Empty;
        public string currentAttributes { get; init; } = string.Empty;
        public string email { get; init; } = string.Empty;
        public string phone { get; init; } = string.Empty;
        public string gender { get; init; } = string.Empty;
        public string ssn { get; init; } = string.Empty;
        public string summary { get; init; } = string.Empty;
        public string countIndex { get; init; } = string.Empty;
        public string accidentTypeId { get; init; } = string.Empty;
        public string currentStatusId { get; init; } = string.Empty;
        public string currentMedicalStatusId { get; init; } = string.Empty;
        public string currentAttributesId { get; init; } = string.Empty;
        public string toGeneratePdf { get; init; } = string.Empty;
        public string switchedDate { get; init; } = string.Empty;
        public string lawFirmId { get; init; } = string.Empty;
        public string caseManagerId { get; init; } = string.Empty;
        public string trackingFollowUpDate { get; init; } = string.Empty;
        public string childSupportLiens { get; init; } = string.Empty;
        public string minorComp { get; init; } = string.Empty;
        public string leadId { get; init; } = string.Empty;
        public string caseManagerDesc { get; init; } = string.Empty;
        public string shareCase { get; init; } = string.Empty;
        public string confirmedWriting { get; init; } = string.Empty;
        public string caseAttorney { get; init; } = string.Empty;
        public string caseAttorneyId { get; init; } = string.Empty;
        public string leadDescription { get; init; } = string.Empty;
        public string caseDropped { get; init; } = string.Empty;
        public string externalCaseId { get; init; } = string.Empty;
        public int totalLiens { get; init; }
        public string lienStatus { get; init; } = string.Empty;
        public string lienStatusId { get; init; } = string.Empty;
        public string settlementStatus { get; init; } = string.Empty;
        public string settlementStatusId { get; init; } = string.Empty;
    }

    public static void MapCaseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/cases")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/", ListCases)
            .RequirePermission(LiensPermissions.CaseRead);

        group.MapGet("/{id:guid}", GetCaseById)
            .RequirePermission(LiensPermissions.CaseRead);

        group.MapGet("/by-number/{caseNumber}", GetCaseByCaseNumber)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy compatibility route from previous service: GET /case/getcaseinfo/{id}
        // under the new base path becomes GET /api/liens/cases/getcaseinfo/{id}.
        group.MapGet("/getcaseinfo/{id:guid}", GetCaseInfoV2Legacy)
            .RequirePermission(LiensPermissions.CaseRead);

        group.MapPost("/", CreateCase)
            .RequirePermission(LiensPermissions.CaseCreate);

        // Legacy compatibility route from previous service: POST /case/v3
        // under the new base path becomes POST /api/liens/cases/v3.
        group.MapPost("/v3", GetCasesV3Legacy)
            .RequirePermission(LiensPermissions.CaseRead);

        group.MapPut("/{id:guid}", UpdateCase)
            .RequirePermission(LiensPermissions.CaseUpdate);

        // Legacy compatibility route from previous service
        group.MapPost("/create", CreateCaseLegacy)
            .RequirePermission(LiensPermissions.CaseCreate);

        group.MapPatch("/update/{id:guid}", UpdateCaseLegacy)
            .RequirePermission(LiensPermissions.CaseUpdate);

        // Legacy compatibility route from previous service: POST /case/liens/facility
        // under the new base path becomes POST /api/liens/cases/liens/facility.
        group.MapPost("/liens/facility", LiensMedicalInformationLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy compatibility route from previous service: POST /case/liens/update-facility
        // under the new base path becomes POST /api/liens/cases/liens/update-facility.
        group.MapPost("/liens/update-facility", UpdateMedicalInformationLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy compatibility route from previous service: GET /case/liens/get-facility/{id}
        // under the new base path becomes GET /api/liens/cases/liens/get-facility/{id}.
        group.MapGet("/liens/get-facility/{id}", GetMedicalInformationLegacy)
            .RequirePermission(LiensPermissions.LienRead);

        // Legacy compatibility route from previous service: POST /case/liens/medical
        // under the new base path becomes POST /api/liens/cases/liens/medical.
        group.MapPost("/liens/medical", LiensMedicaLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy compatibility route from previous service: POST /case/liens/update-medical
        // under the new base path becomes POST /api/liens/cases/liens/update-medical.
        group.MapPost("/liens/update-medical", LiensMedicaUpdateLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy compatibility route from previous service: GET /case/liens/get-medical/{id}
        // under the new base path becomes GET /api/liens/cases/liens/get-medical/{id}.
        group.MapGet("/liens/get-medical/{id}", GetLiensMedicaLegacy)
            .RequirePermission(LiensPermissions.LienRead);

        // Legacy compatibility route from previous service: POST /case/liens/medicalcode
        // under the new base path becomes POST /api/liens/cases/liens/medicalcode.
        group.MapPost("/liens/medicalcode", LiensMedicalCodeLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy compatibility route from previous service: POST /case/liens/update-medicalcode
        // under the new base path becomes POST /api/liens/cases/liens/update-medicalcode.
        group.MapPost("/liens/update-medicalcode", LiensUpdateMedica1lCodeLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy compatibility route from previous service: GET /case/liens/get-medicalcode/{caseId}
        // under the new base path becomes GET /api/liens/cases/liens/get-medicalcode/{caseId}.
        group.MapGet("/liens/get-medicalcode/{caseId}", GetMedicalCodeByCaseIdLegacy)
            .RequirePermission(LiensPermissions.LienRead);

        // Legacy compatibility route from previous service: DELETE /case/liens/delete-medicalcode/{lienId}
        // under the new base path becomes DELETE /api/liens/cases/liens/delete-medicalcode/{lienId}.
        group.MapDelete("/liens/delete-medicalcode/{lienId}", DeleteMedicalCodeByLienIdLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy compatibility route from previous service: POST /case/liens/payment
        // under the new base path becomes POST /api/liens/cases/liens/payment.
        group.MapPost("/liens/payment", UpdateMedicalPayeeOutboundLegacy)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Legacy compatibility route from previous service: GET /case/liens/get-payee-outbound/{liensId}
        // under the new base path becomes GET /api/liens/cases/liens/get-payee-outbound/{liensId}.
        group.MapGet("/liens/get-payee-outbound/{liensId}", GetPayeeOutboundLegacy)
            .RequirePermission(LiensPermissions.LienRead);

        // Legacy compatibility route from previous service: POST /case/casemanager
        // under the new base path becomes POST /api/liens/cases/casemanager.
        group.MapPost("/casemanager", CreateCaseManagerLegacy)
            .RequirePermission(LiensPermissions.CaseCreate);

        // Legacy compatibility route from previous service: POST /case/update-casemanager
        // under the new base path becomes POST /api/liens/cases/update-casemanager.
        group.MapPost("/update-casemanager", UpdateCaseManagerLegacy)
            .RequirePermission(LiensPermissions.CaseUpdate);

        // Legacy compatibility route from previous service: DELETE /case/delete-casemanager/{id}
        // under the new base path becomes DELETE /api/liens/cases/delete-casemanager/{id}.
        group.MapDelete("/delete-casemanager/{id}", DeleteCaseManagerLegacy)
            .RequirePermission(LiensPermissions.CaseUpdate);
    }

    private static async Task<IResult> LiensMedicalInformationLegacy(
        LegacyLiensMedicalInformationFacilityRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.liensId, out var lienId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Invalid or missing liensId.",
            });
        }

        if (!Guid.TryParse(request.facilityId, out var facilityId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Invalid or missing facilityId.",
            });
        }

        var existing = await lienService.GetByIdAsync(tenantId, lienId, ct);
        if (existing is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = $"Lien '{request.liensId}' not found.",
            });
        }

        var mappedRequest = new UpdateLienRequest
        {
            ExternalReference = existing.ExternalReference,
            LienType = existing.LienType,
            CaseId = existing.CaseId,
            FacilityId = facilityId,
            OriginalAmount = existing.OriginalAmount,
            Jurisdiction = existing.Jurisdiction,
            IsConfidential = existing.IsConfidential,
            SubjectFirstName = existing.SubjectFirstName,
            SubjectLastName = existing.SubjectLastName,
            IncidentDate = existing.IncidentDate,
            Description = existing.Description,
        };

        try
        {
            await lienService.UpdateAsync(tenantId, lienId, userId, mappedRequest, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully Updated.",
                data = request.liensId ?? string.Empty,
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

    private static async Task<IResult> UpdateMedicalInformationLegacy(
        LegacyLiensMedicalInformationFacilityRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.liensId, out var lienId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Invalid or missing liensId.",
            });
        }

        if (!Guid.TryParse(request.facilityId, out var facilityId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Invalid or missing facilityId.",
            });
        }

        var existing = await lienService.GetByIdAsync(tenantId, lienId, ct);
        if (existing is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found to update.",
            });
        }

        var mappedRequest = new UpdateLienRequest
        {
            ExternalReference = existing.ExternalReference,
            LienType = existing.LienType,
            CaseId = existing.CaseId,
            FacilityId = facilityId,
            OriginalAmount = existing.OriginalAmount,
            Jurisdiction = existing.Jurisdiction,
            IsConfidential = existing.IsConfidential,
            SubjectFirstName = existing.SubjectFirstName,
            SubjectLastName = existing.SubjectLastName,
            IncidentDate = existing.IncidentDate,
            Description = existing.Description,
        };

        try
        {
            await lienService.UpdateAsync(tenantId, lienId, userId, mappedRequest, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully Updated.",
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

    private static async Task<IResult> GetMedicalInformationLegacy(
        string id,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        if (!Guid.TryParse(id, out var lienId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found.",
            });
        }

        var lien = await lienService.GetByIdAsync(tenantId, lienId, ct);
        if (lien is null || !lien.FacilityId.HasValue)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found.",
            });
        }

        var data = new LegacyLiensMedicalInformationFacilityResponse
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
        };

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Successfully retrieved medical information.",
            data,
        });
    }

    private static async Task<IResult> LiensMedicaLegacy(
        LegacyLiensMedicalRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);

        var hasValidLegacyId = Guid.TryParse(request.id, out var lienId);

        if (hasValidLegacyId)
        {
            var existing = await lienService.GetByIdAsync(tenantId, lienId, ct);
            if (existing is not null)
            {
                var mappedUpdate = new UpdateLienRequest
                {
                    ExternalReference = existing.ExternalReference,
                    LienType = existing.LienType,
                    CaseId = Guid.TryParse(request.caseId, out var parsedCaseId) ? parsedCaseId : existing.CaseId,
                    FacilityId = existing.FacilityId,
                    OriginalAmount = existing.OriginalAmount,
                    Jurisdiction = existing.Jurisdiction,
                    IsConfidential = existing.IsConfidential,
                    SubjectFirstName = existing.SubjectFirstName,
                    SubjectLastName = existing.SubjectLastName,
                    IncidentDate = ParseLegacyDate(request.purchaseDate) ?? existing.IncidentDate,
                    Description = request.note ?? existing.Description,
                };

                try
                {
                    await lienService.UpdateAsync(tenantId, lienId, userId, mappedUpdate, ct);
                    return Results.Ok(new
                    {
                        isSuccess = true,
                        message = "Successfully updated medical record.",
                        data = string.Empty,
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

            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found to update.",
            });
        }

        var mappedCreate = new CreateLienRequest
        {
            LienNumber = $"LM-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            ExternalReference = request.fundingCompanyId,
            LienType = LienType.MedicalLien,
            CaseId = Guid.TryParse(request.caseId, out var createCaseId) ? createCaseId : null,
            FacilityId = null,
            OriginalAmount = 0,
            Jurisdiction = null,
            IsConfidential = false,
            SubjectFirstName = null,
            SubjectLastName = null,
            IncidentDate = ParseLegacyDate(request.purchaseDate),
            Description = request.note,
        };

        try
        {
            var created = await lienService.CreateAsync(tenantId, orgId, userId, mappedCreate, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully created medical record.",
                data = created.Id.ToString(),
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

    private static async Task<IResult> LiensMedicaUpdateLegacy(
        LegacyLiensMedicalRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.id, out var lienId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found to update.",
            });
        }

        var existing = await lienService.GetByIdAsync(tenantId, lienId, ct);
        if (existing is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found to update.",
            });
        }

        var mappedUpdate = new UpdateLienRequest
        {
            ExternalReference = existing.ExternalReference,
            LienType = existing.LienType,
            CaseId = Guid.TryParse(request.caseId, out var parsedCaseId) ? parsedCaseId : existing.CaseId,
            FacilityId = existing.FacilityId,
            OriginalAmount = existing.OriginalAmount,
            Jurisdiction = existing.Jurisdiction,
            IsConfidential = existing.IsConfidential,
            SubjectFirstName = existing.SubjectFirstName,
            SubjectLastName = existing.SubjectLastName,
            IncidentDate = ParseLegacyDate(request.purchaseDate) ?? existing.IncidentDate,
            Description = request.note ?? existing.Description,
        };

        try
        {
            await lienService.UpdateAsync(tenantId, lienId, userId, mappedUpdate, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully updated medical record.",
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

    private static async Task<IResult> GetLiensMedicaLegacy(
        string id,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        if (!Guid.TryParse(id, out var lienId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found.",
            });
        }

        var lien = await lienService.GetByIdAsync(tenantId, lienId, ct);
        if (lien is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found.",
            });
        }

        var data = new LegacyLiensMedicalResponse
        {
            id = lien.Id.ToString(),
            caseId = lien.CaseId?.ToString() ?? string.Empty,
            status = lien.Status,
            purchaseDate = FormatLegacyDate(lien.IncidentDate),
            initialServiceDate = string.Empty,
            endServiceDate = string.Empty,
            note = lien.Description ?? string.Empty,
            created = FormatLegacyTimestamp(lien.CreatedAtUtc),
            createdBy = string.Empty,
            updated = FormatLegacyTimestamp(lien.UpdatedAtUtc),
            updatedBy = string.Empty,
            fundingCompanyId = lien.ExternalReference ?? string.Empty,
            fundingCompany = string.Empty,
            isBulk = string.Empty,
            isServicing = string.Empty,
        };

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Successfully retrieved medical record.",
            data,
        });
    }

    private static async Task<IResult> LiensMedicalCodeLegacy(
        LegacyLiensMedicalCodeRequest request,
        ILienService lienService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.liensId, out var lienId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Invalid or missing liensId.",
            });
        }

        var lien = await lienService.GetByIdAsync(tenantId, lienId, ct);
        if (lien is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = $"Lien '{request.liensId}' not found.",
            });
        }

        var details =
            $"code={request.code ?? string.Empty}; " +
            $"medicareCost={request.medicareCost ?? string.Empty}; " +
            $"billingAmount={request.billingAmount ?? string.Empty}; " +
            $"purchaseAmount={request.purchaseAmount ?? string.Empty}; " +
            $"payee={request.payee ?? string.Empty}; " +
            $"outboundCheckNumber={request.outboundCheckNumber ?? string.Empty}";

        var mapped = new CreateServicingItemRequest
        {
            TaskNumber = $"LMC-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            TaskType = "LegacyMedicalCode",
            Description = string.IsNullOrWhiteSpace(request.code)
                ? "Legacy medical code entry"
                : $"Medical code {request.code}",
            AssignedTo = "system",
            CaseId = lien.CaseId,
            LienId = lien.Id,
            Notes = details,
        };

        try
        {
            var created = await servicingItemService.CreateAsync(tenantId, orgId, userId, mapped, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully created medical code record.",
                data = created.Id.ToString(),
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

    private static async Task<IResult> LiensUpdateMedica1lCodeLegacy(
        LegacyLiensMedicalCodeRequest request,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.id, out var medicalCodeId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found to update.",
            });
        }

        var existing = await servicingItemService.GetByIdAsync(tenantId, medicalCodeId, ct);
        if (existing is null || !string.Equals(existing.TaskType, "LegacyMedicalCode", StringComparison.Ordinal))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found to update.",
            });
        }

        if (Guid.TryParse(request.liensId, out var lienId) && existing.LienId != lienId)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No record found to update.",
            });
        }

        var details =
            $"code={request.code ?? string.Empty}; " +
            $"medicareCost={request.medicareCost ?? string.Empty}; " +
            $"billingAmount={request.billingAmount ?? string.Empty}; " +
            $"purchaseAmount={request.purchaseAmount ?? string.Empty}; " +
            $"payee={request.payee ?? string.Empty}; " +
            $"outboundCheckNumber={request.outboundCheckNumber ?? string.Empty}";

        var mapped = new UpdateServicingItemRequest
        {
            TaskType = existing.TaskType,
            Description = string.IsNullOrWhiteSpace(request.code)
                ? existing.Description
                : $"Medical code {request.code}",
            AssignedTo = string.IsNullOrWhiteSpace(existing.AssignedTo) ? "system" : existing.AssignedTo,
            AssignedToUserId = existing.AssignedToUserId,
            Priority = existing.Priority,
            Status = existing.Status,
            CaseId = existing.CaseId,
            LienId = existing.LienId,
            DueDate = existing.DueDate,
            Notes = details,
            Resolution = existing.Resolution,
        };

        try
        {
            await servicingItemService.UpdateAsync(tenantId, medicalCodeId, userId, mapped, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully updated medical code record.",
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

    private static async Task<IResult> GetMedicalCodeByCaseIdLegacy(
        string caseId,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        if (!Guid.TryParse(caseId, out var parsedCaseId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No records found.",
            });
        }

        var results = await servicingItemService.SearchAsync(
            tenantId,
            search: "LegacyMedicalCode",
            status: null,
            priority: null,
            assignedTo: null,
            caseId: parsedCaseId,
            lienId: null,
            page: 1,
            pageSize: 500,
            ct);

        var data = results.Items
            .Where(i => string.Equals(i.TaskType, "LegacyMedicalCode", StringComparison.Ordinal))
            .Select(i =>
            {
                var fields = ParseLegacyNoteFields(i.Notes);
                return new LegacyLiensMedicalCodeResponse
                {
                    id = i.Id.ToString(),
                    liensId = i.LienId?.ToString() ?? string.Empty,
                    code = fields.GetValueOrDefault("code", string.Empty),
                    medicareCost = fields.GetValueOrDefault("medicareCost", string.Empty),
                    billingAmount = fields.GetValueOrDefault("billingAmount", string.Empty),
                    purchaseAmount = fields.GetValueOrDefault("purchaseAmount", string.Empty),
                    payee = fields.GetValueOrDefault("payee", string.Empty),
                    outboundCheckNumber = fields.GetValueOrDefault("outboundCheckNumber", string.Empty),
                    created = FormatLegacyTimestamp(i.CreatedAtUtc),
                    createdBy = string.Empty,
                    updated = FormatLegacyTimestamp(i.UpdatedAtUtc),
                    updatedBy = string.Empty,
                };
            })
            .ToList();

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Successfully retrieved medical code records.",
            data,
        });
    }

    private static async Task<IResult> DeleteMedicalCodeByLienIdLegacy(
        string lienId,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(lienId, out var parsedLienId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No records found.",
            });
        }

        var results = await servicingItemService.SearchAsync(
            tenantId,
            search: "LegacyMedicalCode",
            status: null,
            priority: null,
            assignedTo: null,
            caseId: null,
            lienId: parsedLienId,
            page: 1,
            pageSize: 500,
            ct);

        var targets = results.Items
            .Where(i => string.Equals(i.TaskType, "LegacyMedicalCode", StringComparison.Ordinal))
            .ToList();

        if (targets.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No records found.",
            });
        }

        try
        {
            foreach (var item in targets)
                await servicingItemService.DeleteAsync(tenantId, item.Id, userId, ct);

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully deleted medical code record(s).",
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

    private static async Task<IResult> UpdateMedicalPayeeOutboundLegacy(
        LegacyPayeeOutboundRequest request,
        ILienService lienService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.liensId, out var lienId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Invalid or missing liensId.",
            });
        }

        var lien = await lienService.GetByIdAsync(tenantId, lienId, ct);
        if (lien is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = $"Lien '{request.liensId}' not found.",
            });
        }

        var details =
            $"payee={request.payee ?? string.Empty}; " +
            $"outboundCheckNumber={request.outboundCheckNumber ?? string.Empty}";

        try
        {
            var existingItems = await servicingItemService.SearchAsync(
                tenantId,
                search: "LegacyMedicalPayment",
                status: null,
                priority: null,
                assignedTo: null,
                caseId: lien.CaseId,
                lienId: lienId,
                page: 1,
                pageSize: 100,
                ct);

            var existing = existingItems.Items.FirstOrDefault(i =>
                string.Equals(i.TaskType, "LegacyMedicalPayment", StringComparison.Ordinal) &&
                i.LienId == lienId);

            if (existing is null)
            {
                var createRequest = new CreateServicingItemRequest
                {
                    TaskNumber = $"LMP-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    TaskType = "LegacyMedicalPayment",
                    Description = "Legacy medical payee/outbound",
                    AssignedTo = "system",
                    CaseId = lien.CaseId,
                    LienId = lienId,
                    Notes = details,
                };

                await servicingItemService.CreateAsync(tenantId, orgId, userId, createRequest, ct);
                return Results.Ok(new
                {
                    isSuccess = true,
                    message = "Successfully inserted payee and outbound check number.",
                });
            }

            var updateRequest = new UpdateServicingItemRequest
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
                Notes = details,
                Resolution = existing.Resolution,
            };

            await servicingItemService.UpdateAsync(tenantId, existing.Id, userId, updateRequest, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully updated payee and outbound check number.",
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

    private static async Task<IResult> GetPayeeOutboundLegacy(
        string liensId,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        if (!Guid.TryParse(liensId, out var lienId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: Unable to retrieve Payee and Outbound Check Number.",
            });
        }

        var results = await servicingItemService.SearchAsync(
            tenantId,
            search: "LegacyMedicalPayment",
            status: null,
            priority: null,
            assignedTo: null,
            caseId: null,
            lienId: lienId,
            page: 1,
            pageSize: 100,
            ct);

        var item = results.Items.FirstOrDefault(i =>
            string.Equals(i.TaskType, "LegacyMedicalPayment", StringComparison.Ordinal) &&
            i.LienId == lienId);

        if (item is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: Unable to retrieve Payee and Outbound Check Number.",
            });
        }

        var fields = ParseLegacyNoteFields(item.Notes);
        var data = new LegacyPayeeOutboundResponse
        {
            id = item.Id.ToString(),
            liensId = item.LienId?.ToString() ?? string.Empty,
            payee = fields.GetValueOrDefault("payee", string.Empty),
            outboundCheckNumber = fields.GetValueOrDefault("outboundCheckNumber", string.Empty),
        };

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Successfully retrieved Payee and Outbound Check Number.",
            data,
        });
    }

    private static async Task<IResult> CreateCaseManagerLegacy(
        LegacyCaseManagerRequest request,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);

        var isLawyer = string.Equals(request.roleId, "7", StringComparison.Ordinal);

        var mapped = new CreateContactRequest
        {
            ContactType = ContactType.CaseManager,
            FirstName = request.firstname ?? string.Empty,
            LastName = request.lastname ?? string.Empty,
            Email = request.email,
            Phone = request.phone,
            Organization = request.lawfirmId,
            Notes = string.IsNullOrWhiteSpace(request.roleId)
                ? null
                : $"roleId={request.roleId}",
        };

        try
        {
            var created = await contactService.CreateAsync(tenantId, orgId, userId, mapped, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = isLawyer
                    ? "Successfully created Lawyer."
                    : "Successfully created Case Manager.",
                data = created.Id.ToString(),
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

    private static async Task<IResult> UpdateCaseManagerLegacy(
        LegacyCaseManagerUpdateRequest request,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.id, out var contactId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: Updating Case Manager.",
            });
        }

        var existing = await contactService.GetByIdAsync(tenantId, contactId, ct);
        if (existing is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: Updating Case Manager.",
            });
        }

        var isLawyer = string.Equals(request.roleId, "7", StringComparison.Ordinal);

        var mapped = new UpdateContactRequest
        {
            ContactType = ContactType.CaseManager,
            FirstName = request.firstname ?? existing.FirstName,
            LastName = request.lastname ?? existing.LastName,
            Email = request.email ?? existing.Email,
            Phone = request.phone ?? existing.Phone,
            Organization = request.lawfirmId ?? existing.Organization,
            Title = existing.Title,
            Fax = existing.Fax,
            Website = existing.Website,
            AddressLine1 = existing.AddressLine1,
            City = existing.City,
            State = existing.State,
            PostalCode = existing.PostalCode,
            Notes = string.IsNullOrWhiteSpace(request.roleId)
                ? existing.Notes
                : $"roleId={request.roleId}",
        };

        try
        {
            await contactService.UpdateAsync(tenantId, contactId, userId, mapped, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = isLawyer
                    ? "Successfully updated Lawyer."
                    : "Successfully updated Case Manager.",
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

    private static async Task<IResult> DeleteCaseManagerLegacy(
        string id,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(id, out var contactId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: Delete Case Manager.",
            });
        }

        var existing = await contactService.GetByIdAsync(tenantId, contactId, ct);
        if (existing is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: Delete Case Manager.",
            });
        }

        try
        {
            await contactService.DeactivateAsync(tenantId, contactId, userId, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully Delete Case Manager.",
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

    private static DateOnly? ParseLegacyDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParseExact(
            value,
            "MM/dd/yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static string? BuildAddress(string? address, string? city, string? state, string? zipcode)
    {
        var formatted = string.Join(", ", new[] { address, city, state, zipcode }
            .Where(v => !string.IsNullOrWhiteSpace(v)));

        return string.IsNullOrWhiteSpace(formatted) ? null : formatted;
    }

    private static (string Address, string City, string State, string Zipcode) SplitLegacyAddress(string? rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
            return (string.Empty, string.Empty, string.Empty, string.Empty);

        var parts = rawAddress
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length >= 4)
        {
            return (
                string.Join(", ", parts.Take(parts.Length - 3)),
                parts[^3],
                parts[^2],
                parts[^1]);
        }

        if (parts.Length == 3)
            return (parts[0], parts[1], parts[2], string.Empty);

        if (parts.Length == 2)
            return (parts[0], parts[1], string.Empty, string.Empty);

        return (rawAddress.Trim(), string.Empty, string.Empty, string.Empty);
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

    private static async Task<IResult> ListCases(
        ICaseService caseService,
        ICurrentRequestContext ctx,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await caseService.SearchAsync(tenantId, search, status, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCaseById(
        Guid id,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await caseService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Case '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetCaseByCaseNumber(
        string caseNumber,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await caseService.GetByCaseNumberAsync(tenantId, caseNumber, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Case with number '{caseNumber}' not found." } })
            : Results.Ok(result);
    }

    private static string FormatLegacyDate(DateOnly? value)
        => value?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatLegacyTimestamp(DateTime value)
        => value.ToString("MM/dd/yyyy hh:mm tt", CultureInfo.InvariantCulture);

    private static async Task<IResult> GetCaseInfoV2Legacy(
        Guid id,
        ICaseService caseService,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var item = await caseService.GetByIdAsync(tenantId, id, ct);
        if (item is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No cases found.",
            });
        }

        var lienResult = await lienService.SearchAsync(
            tenantId,
            search: null,
            status: null,
            lienType: null,
            caseId: id,
            facilityId: null,
            page: 1,
            pageSize: 100,
            ct);

        var openLiens = 0;
        foreach (var openStatus in LienStatus.Open)
        {
            var openResult = await lienService.SearchAsync(
                tenantId,
                search: null,
                status: openStatus,
                lienType: null,
                caseId: id,
                facilityId: null,
                page: 1,
                pageSize: 1,
                ct);
            openLiens += openResult.TotalCount;
        }

        var totalLiens = lienResult.TotalCount;
        var showClosedOnlyStatus = totalLiens > 0 && openLiens == 0;
        var latestTerminalLien = lienResult.Items
            .Where(l => LienStatus.Terminal.Contains(l.Status))
            .OrderByDescending(l => l.CreatedAtUtc)
            .FirstOrDefault();

        var parsedAddress = SplitLegacyAddress(item.ClientAddress);

        var legacyItem = new LegacyCaseInfoV2Response
        {
            caseId = item.Id.ToString(),
            caseCode = item.CaseNumber,
            firstname = item.ClientFirstName,
            lastname = item.ClientLastName,
            dateOfBirth = FormatLegacyDate(item.ClientDob),
            address = parsedAddress.Address,
            city = parsedAddress.City,
            state = parsedAddress.State,
            zipcode = parsedAddress.Zipcode,
            isServicing = string.Empty,
            isUccFiled = string.Empty,
            isBulk = string.Empty,
            accidentType = string.Empty,
            accidentState = string.Empty,
            dateOfLoss = FormatLegacyDate(item.DateOfIncident),
            lawFirm = string.Empty,
            caseManager = string.Empty,
            note = item.Notes ?? string.Empty,
            created = FormatLegacyTimestamp(item.CreatedAtUtc),
            createBy = string.Empty,
            updated = FormatLegacyTimestamp(item.UpdatedAtUtc),
            updateBy = string.Empty,
            status = item.Status,
            currentStatus = item.Status,
            currentMedicalStatus = string.Empty,
            currentAttributes = string.Empty,
            email = item.ClientEmail ?? string.Empty,
            phone = item.ClientPhone ?? string.Empty,
            gender = string.Empty,
            ssn = string.Empty,
            summary = item.Description ?? string.Empty,
            countIndex = string.Empty,
            accidentTypeId = string.Empty,
            currentStatusId = string.Empty,
            currentMedicalStatusId = string.Empty,
            currentAttributesId = string.Empty,
            toGeneratePdf = string.Empty,
            switchedDate = string.Empty,
            lawFirmId = string.Empty,
            caseManagerId = string.Empty,
            trackingFollowUpDate = string.Empty,
            childSupportLiens = string.Empty,
            minorComp = string.Empty,
            leadId = string.Empty,
            caseManagerDesc = string.Empty,
            shareCase = string.Empty,
            confirmedWriting = string.Empty,
            caseAttorney = string.Empty,
            caseAttorneyId = string.Empty,
            leadDescription = string.Empty,
            caseDropped = string.Empty,
            externalCaseId = item.ExternalReference ?? string.Empty,
            totalLiens = totalLiens,
            lienStatus = showClosedOnlyStatus ? latestTerminalLien?.Status ?? string.Empty : string.Empty,
            lienStatusId = showClosedOnlyStatus ? latestTerminalLien?.Status ?? string.Empty : string.Empty,
            settlementStatus = string.Empty,
            settlementStatusId = string.Empty,
        };

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Case Info.",
            data = new[] { legacyItem },
        });
    }

    private static async Task<IResult> CreateCase(
        CreateCaseRequest request,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await caseService.CreateAsync(tenantId, orgId, userId, request, ct);
        return Results.Created($"/api/liens/cases/{result.Id}", result);
    }

    private static async Task<IResult> GetCasesV3Legacy(
        LegacyCaseV3FilterRequest filter,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        var page = filter.page < 1 ? 1 : filter.page;
        var limit = filter.limit < 1 ? 20 : filter.limit;

        var result = await caseService.SearchV3Async(
            tenantId,
            filter.keyword,
            filter.statusId,
            page,
            limit,
            filter.sortBy,
            filter.sortDirection,
            ct);

        if (result.TotalCount == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No cases found.",
            });
        }

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Case List.",
            data = result.Items,
            page = result.Page,
            limit = result.PageSize,
            totalCount = result.TotalCount,
        });
    }

    private static async Task<IResult> CreateCaseLegacy(
        LegacyCreateCaseRequest request,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);

        var mappedRequest = new CreateCaseRequest
        {
            CaseNumber = string.IsNullOrWhiteSpace(request.code)
                ? $"CASE-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
                : request.code,
            ClientFirstName = request.firstname ?? string.Empty,
            ClientLastName = request.lastname ?? string.Empty,
            ExternalReference = request.externalCaseId,
            ClientDob = ParseLegacyDate(request.dob),
            ClientAddress = BuildAddress(request.address, request.city, request.state, request.zipcode),
            DateOfIncident = ParseLegacyDate(request.dateOfLoss),
            Notes = request.note,
        };

        var result = await caseService.CreateAsync(tenantId, orgId, userId, mappedRequest, ct);

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Successfully Created.",
            data = new Dictionary<string, string>
            {
                ["id"] = result.Id.ToString(),
            },
        });
    }

    private static async Task<IResult> UpdateCaseLegacy(
        string id,
        LegacyUpdateCaseRequest request,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(id, out var caseId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = $"Case '{id}' not found.",
            });
        }

        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        var existing = await caseService.GetByIdAsync(tenantId, caseId, ct);
        if (existing is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = $"Case '{id}' not found.",
            });
        }

        var mappedRequest = new UpdateCaseRequest
        {
            ClientFirstName = request.firstname ?? string.Empty,
            ClientLastName = request.lastname ?? string.Empty,
            ExternalReference = request.externalCaseId,
            ClientDob = ParseLegacyDate(request.dob),
            ClientAddress = BuildAddress(request.address, request.city, request.state, request.zipcode),
            DateOfIncident = ParseLegacyDate(request.dateOfLoss),
            Notes = request.note,
        };

        var isNoChanges =
            string.Equals(existing.ClientFirstName, mappedRequest.ClientFirstName, StringComparison.Ordinal) &&
            string.Equals(existing.ClientLastName, mappedRequest.ClientLastName, StringComparison.Ordinal) &&
            string.Equals(existing.ExternalReference, mappedRequest.ExternalReference, StringComparison.Ordinal) &&
            existing.ClientDob == mappedRequest.ClientDob &&
            string.Equals(existing.ClientAddress, mappedRequest.ClientAddress, StringComparison.Ordinal) &&
            existing.DateOfIncident == mappedRequest.DateOfIncident &&
            string.Equals(existing.Notes, mappedRequest.Notes, StringComparison.Ordinal);

        if (isNoChanges)
        {
            return Results.Ok(new
            {
                isSuccess = true,
                message = "No changes detected.",
            });
        }

        await caseService.UpdateAsync(tenantId, caseId, userId, mappedRequest, ct);

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Successfully Updated.",
        });
    }

    private static async Task<IResult> UpdateCase(
        Guid id,
        UpdateCaseRequest request,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await caseService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }
}
