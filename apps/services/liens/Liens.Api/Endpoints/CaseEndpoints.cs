using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;
using System.Text;
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

    private sealed class LegacyLawFirmV3Request
    {
        public string? LawFirmId { get; init; }
        public string? Keyword { get; init; }
        public int Page { get; init; } = 1;
        public int Limit { get; init; } = 10;
    }

    private sealed class LegacyMedicalLiensV3Request
    {
        public string? MedicalId { get; init; }
        public string? Keyword { get; init; }
        public int Page { get; init; } = 1;
        public int Limit { get; init; } = 10;
    }

    private sealed class LegacyFundingCompanyLiensV3Request
    {
        public string? FundingCompanyId { get; init; }
        public string? Keyword { get; init; }
        public int Page { get; init; } = 1;
        public int Limit { get; init; } = 10;
    }

    private sealed class LegacyFacilityLiensV3Request
    {
        public string? FacilityId { get; init; }
        public string? Keyword { get; init; }
        public int Page { get; init; } = 1;
        public int Limit { get; init; } = 10;
    }

    private sealed class LegacyLeadCaseV3Request
    {
        public string? LeadId { get; init; }
        public string? Keyword { get; init; }
        public int Page { get; init; } = 1;
        public int Limit { get; init; } = 10;
    }

    private sealed class LegacyCaseUpdatesV3Request
    {
        public string? CaseId { get; init; }
        public int Page { get; init; } = 1;
        public int Limit { get; init; } = 10;
    }

    private sealed class LegacyLiensUpdatesV3Request
    {
        public string? CaseId { get; init; }
        public int Page { get; init; } = 1;
        public int Limit { get; init; } = 10;
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

    private sealed class LegacyReassignLawFirmRequest
    {
        public string? caseId { get; init; }
        public string? lawfirm { get; init; }
    }

    private sealed class LegacyReassignCaseManagerRequest
    {
        public string? caseId { get; init; }
        public string? caseManager { get; init; }
    }

    private sealed class LegacyReassignLeadRequest
    {
        public string? caseId { get; init; }
        public string? leadId { get; init; }
    }

    private sealed class LegacyBatchReassignRequest
    {
        public string? contactType { get; init; }
        public string? oldId { get; init; }
        public string? newId { get; init; }
    }

    private sealed class LegacyGenerateCaseCsvRequest
    {
        public string? caseId { get; init; }
        public string? lawFirmId { get; init; }
        public string? accidentTypeId { get; init; }
        public string? statusId { get; init; }
        public string? caseManagerId { get; init; }
    }

    private sealed class LegacyGenerateLiensCsvRequest
    {
        public string? caseId { get; init; }
        public string? liensId { get; init; }
        public string? lawFirmId { get; init; }
        public string? medicalFacilityId { get; init; }
        public string? purchaseDate { get; init; }
        public string? caseManagerId { get; init; }
        public string? lienStatusId { get; init; }
    }

    private sealed class LegacyLiensCsvRow
    {
        public string CaseCode { get; init; } = string.Empty;
        public string LiensCode { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string PurchaseDate { get; init; } = string.Empty;
        public string InitialServiceDate { get; init; } = string.Empty;
        public string EndServiceDate { get; init; } = string.Empty;
        public string Note { get; init; } = string.Empty;
        public string FacilityEmail { get; init; } = string.Empty;
        public string FacilityPhone { get; init; } = string.Empty;
        public string TotalPurchase { get; init; } = string.Empty;
        public string TotalBilling { get; init; } = string.Empty;
        public string LawFirm { get; init; } = string.Empty;
        public string CaseManager { get; init; } = string.Empty;
        public string FacilityName { get; init; } = string.Empty;
        public string FacilityContactName { get; init; } = string.Empty;
        public string MedicalProvider { get; init; } = string.Empty;
        public string PlainTiffName { get; init; } = string.Empty;
        public string ClosedDate { get; init; } = string.Empty;
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

        // Legacy compatibility route from previous service: GET /case/law/{lawFirmId}/{isTotal?}
        // under the new base path becomes GET /api/liens/cases/law/{lawFirmId}/{isTotal?}.
        group.MapGet("/law/{lawFirmId}/{isTotal?}", GetCaseByLawFirmIdLegacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy compatibility route from previous service: POST /case/law/v3
        // under the new base path becomes POST /api/liens/cases/law/v3.
        group.MapPost("/law/v3", GetLawFirmV3Legacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy compatibility route from previous service: POST /case/medical/v3
        // under the new base path becomes POST /api/liens/cases/medical/v3.
        group.MapPost("/medical/v3", GetLiensByMedicalIdV3Legacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy compatibility route from previous service: POST /case/funding/v3
        // under the new base path becomes POST /api/liens/cases/funding/v3.
        group.MapPost("/funding/v3", GetLiensByFundingCompanyIdV3Legacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy compatibility route from previous service: POST /case/medical/facility/v3
        // under the new base path becomes POST /api/liens/cases/medical/facility/v3.
        group.MapPost("/medical/facility/v3", GetLiensByMedicalFacilityIdV3Legacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy compatibility route from previous service: POST /case/leads/v3
        // under the new base path becomes POST /api/liens/cases/leads/v3.
        group.MapPost("/leads/v3", GetLeadV3Legacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy compatibility route from previous service: POST /case/case-updates/v3
        // under the new base path becomes POST /api/liens/cases/case-updates/v3.
        group.MapPost("/case-updates/v3", GetCaseUpdatesV3Legacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy compatibility route from previous service: POST /case/liens-updates/v3
        // under the new base path becomes POST /api/liens/cases/liens-updates/v3.
        group.MapPost("/liens-updates/v3", GetLiensUpdatesV3Legacy)
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

        // Legacy compatibility route from previous service: POST /case/reassign/lawfirm
        // under the new base path becomes POST /api/liens/cases/reassign/lawfirm.
        group.MapPost("/reassign/lawfirm", ReassignLawfirmLegacy)
            .RequirePermission(LiensPermissions.CaseUpdate);

        // Legacy compatibility route from previous service: POST /case/reassign/casemanager
        // under the new base path becomes POST /api/liens/cases/reassign/casemanager.
        group.MapPost("/reassign/casemanager", ReassignCaseManagerLegacy)
            .RequirePermission(LiensPermissions.CaseUpdate);

        // Legacy compatibility route from previous service: POST /case/reassign/leads
        // under the new base path becomes POST /api/liens/cases/reassign/leads.
        group.MapPost("/reassign/leads", ReassignLeadLegacy)
            .RequirePermission(LiensPermissions.CaseUpdate);

        // Legacy compatibility route from previous service: POST /case/batch-reassign
        // under the new base path becomes POST /api/liens/cases/batch-reassign.
        group.MapPost("/batch-reassign", BatchReassignLawfirmLegacy)
            .RequirePermission(LiensPermissions.CaseUpdate);

        // Legacy compatibility route from previous service: GET /case/payoff-quote/{caseId}
        // under the new base path becomes GET /api/liens/cases/payoff-quote/{caseId}.
        group.MapGet("/payoff-quote/{caseId:guid}", GeneratePayoffQuoteLegacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy compatibility route from previous service: GET /case/dashboard/piechart
        // under the new base path becomes GET /api/liens/cases/dashboard/piechart.
        group.MapGet("/dashboard/piechart", GetDashboardLegacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy compatibility route from previous service: POST /case/generate-csv
        // under the new base path becomes POST /api/liens/cases/generate-csv.
        group.MapPost("/generate-csv", GenerateCaseCsvLegacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy compatibility route from previous service: POST /case/liens/generate-csv
        // under the new base path becomes POST /api/liens/cases/liens/generate-csv.
        group.MapPost("/liens/generate-csv", GenerateLiensCsvLegacy)
            .RequirePermission(LiensPermissions.LienRead);

        // ── Partial update variants ───────────────────────────────────────────
        group.MapPatch("/personal-update", UpdatePersonalInfo)
            .RequirePermission(LiensPermissions.CaseUpdate);
        group.MapPatch("/primary-update", UpdatePrimaryInfo)
            .RequirePermission(LiensPermissions.CaseUpdate);
        group.MapPatch("/details-update", UpdateCaseDetails)
            .RequirePermission(LiensPermissions.CaseUpdate);

        // ── Linked-entity filter routes ───────────────────────────────────────
        group.MapGet("/medical/{medicalId}", GetCasesByLinkedEntity)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/funding/{fundingCompanyId}", GetCasesByLinkedEntity)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/medical/facility/{facilityId}", GetCasesByLinkedEntity)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/case-manager/{caseManagerId}", GetCasesByLinkedEntity)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/medical/facility-contact/{facilityContactId}", GetCasesByLinkedEntity)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/lead/{leadId}", GetCasesByLinkedEntity)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/leads/{leadId}", GetCasesByLinkedEntity)
            .RequirePermission(LiensPermissions.CaseRead);

        // ── Audit log ─────────────────────────────────────────────────────────
        group.MapGet("/case-updates/{caseId:guid}", GetCaseAuditLog)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/liens-updates/{caseId:guid}", GetLiensAuditLog)
            .RequirePermission(LiensPermissions.LienRead);

        // ── Liens management from case context ────────────────────────────────
        group.MapPost("/liens", ListLiensByCaseContext)
            .RequirePermission(LiensPermissions.LienRead);
        group.MapPost("/liens/{caseId:guid}", ListLiensByCaseId)
            .RequirePermission(LiensPermissions.LienRead);
        group.MapPost("/liens/v3", SearchLiensV3)
            .RequirePermission(LiensPermissions.LienRead);
        group.MapPost("/liens/details/{caseId:guid}", GetLiensDetailsByCaseId)
            .RequirePermission(LiensPermissions.LienRead);
        group.MapDelete("/liens/delete/{liensId:guid}", DeleteLien)
            .RequirePermission(LiensPermissions.LienUpdate);

        // ── Manual medical codes ──────────────────────────────────────────────
        group.MapPost("/manual/medical/code/create", CreateManualMedicalCode)
            .RequirePermission(LiensPermissions.LienUpdate);
        group.MapPost("/manual/medical/code/update", UpdateManualMedicalCode)
            .RequirePermission(LiensPermissions.LienUpdate);

        // ── Dashboard extended ────────────────────────────────────────────────
        group.MapGet("/dashboard/task-summary", GetDashboardTaskSummary)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/dashboard/total-lien-report-export", GetTotalLienReport)
            .RequirePermission(LiensPermissions.LienRead);
        group.MapPost("/dashboard/total-lien-report-export/v3", GetTotalLienReportV3)
            .RequirePermission(LiensPermissions.LienRead);
        group.MapGet("/dashboard/total-case-report-export", GetTotalCaseReport)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapPost("/dashboard/total-case-report-export/v3", GetTotalCaseReportV3)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/dashboard/lawfirm-case-report-export", GetLawFirmCaseReport)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapPost("/dashboard/lawfirm-case-report-export/v3", GetLawFirmCaseReportV3)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/dashboard/medical-provider-report-export", GetMedicalProviderReport)
            .RequirePermission(LiensPermissions.LienRead);
        group.MapPost("/dashboard/medical-provider-report-export/v3", GetMedicalProviderReportV3)
            .RequirePermission(LiensPermissions.LienRead);

        // Report CSV exports
        group.MapGet("/lien-report-csv", GetLienReportCsv)
            .RequirePermission(LiensPermissions.LienRead);
        group.MapGet("/case-report-csv", GetCaseReportCsv)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/law-firm-case-report-csv", GetLawFirmCaseReportCsv)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/medical-provider-case-report-csv", GetMedicalProviderCaseReportCsv)
            .RequirePermission(LiensPermissions.LienRead);

        // ── CSV imports ───────────────────────────────────────────────────────
        group.MapPost("/import-csv", ImportCsv)
            .RequirePermission(LiensPermissions.CaseCreate);
        group.MapPost("/migrate-csv", ImportCsv)
            .RequirePermission(LiensPermissions.CaseCreate);
        group.MapPost("/migrate-guardian-csv", ImportCsv)
            .RequirePermission(LiensPermissions.CaseCreate);
        group.MapPost("/update-lien-payment-csv", ImportCsv)
            .RequirePermission(LiensPermissions.LienUpdate);

        // ── Document type management ──────────────────────────────────────────
        group.MapPost("/document/type", AddDocumentType)
            .RequirePermission(LiensPermissions.CaseUpdate);

        // ── Global search ─────────────────────────────────────────────────────
        group.MapPost("/global-search", GlobalSearch)
            .RequirePermission(LiensPermissions.CaseRead);

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
        IServicingItemService servicingItemService,
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

        var info = infoResult.Items.FirstOrDefault(i =>
            string.Equals(i.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal) &&
            i.LienId == lienId);

        var infoFields = ParseLegacyNoteFields(info?.Notes);

        var data = new LegacyLiensMedicalInformationFacilityResponse
        {
            id = string.Empty,
            liensId = lien.Id.ToString(),
            facilityId = lien.FacilityId.Value.ToString(),
            facilityContactId = string.Empty,
            email = string.Empty,
            phone = string.Empty,
            medicalProviderId = infoFields.GetValueOrDefault("medicalProviderId", string.Empty),
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

    private static string SerializeLegacyNoteFields(Dictionary<string, string> fields)
    {
        if (fields.Count == 0)
            return string.Empty;

        return string.Join("; ", fields.Select(pair => $"{pair.Key}={pair.Value}"));
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

    private static async Task<IResult> ReassignLawfirmLegacy(
        LegacyReassignLawFirmRequest request,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.caseId, out var caseId) ||
            !Guid.TryParse(request.lawfirm, out var lawFirmOrgId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned case.",
            });
        }

        var isSuccess = await caseService.ReassignLawFirmAsync(
            tenantId,
            caseId,
            lawFirmOrgId,
            userId,
            ct);

        if (!isSuccess)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned case.",
            });
        }

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Successfully re-assigned case to new law firm.",
        });
    }

    private static async Task<IResult> ReassignCaseManagerLegacy(
        LegacyReassignCaseManagerRequest request,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.caseId, out var caseId) ||
            !Guid.TryParse(request.caseManager, out var caseManagerId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned case.",
            });
        }

        var isSuccess = await caseService.ReassignCaseManagerAsync(
            tenantId,
            caseId,
            caseManagerId,
            userId,
            ct);

        if (!isSuccess)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned case.",
            });
        }

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Successfully re-assigned case to new case manager.",
        });
    }

    private static async Task<IResult> ReassignLeadLegacy(
        LegacyReassignLeadRequest request,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.caseId, out var caseId) ||
            string.IsNullOrWhiteSpace(request.leadId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned case.",
            });
        }

        var existing = await caseService.GetByIdAsync(tenantId, caseId, ct);
        if (existing is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned case.",
            });
        }

        try
        {
            var fields = ParseLegacyNoteFields(existing.Notes);
            fields["leadId"] = request.leadId.Trim();

            var update = new UpdateCaseRequest
            {
                ClientFirstName = existing.ClientFirstName,
                ClientLastName = existing.ClientLastName,
                ExternalReference = existing.ExternalReference,
                Title = existing.Title,
                ClientDob = existing.ClientDob,
                ClientPhone = existing.ClientPhone,
                ClientEmail = existing.ClientEmail,
                ClientAddress = existing.ClientAddress,
                DateOfIncident = existing.DateOfIncident,
                InsuranceCarrier = existing.InsuranceCarrier,
                PolicyNumber = existing.PolicyNumber,
                ClaimNumber = existing.ClaimNumber,
                Description = existing.Description,
                Notes = SerializeLegacyNoteFields(fields),
                Status = existing.Status,
                DemandAmount = existing.DemandAmount,
                SettlementAmount = existing.SettlementAmount,
            };

            await caseService.UpdateAsync(tenantId, caseId, userId, update, ct);

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully re-assigned case to new lead.",
            });
        }
        catch
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assigned case.",
            });
        }
    }

    private static async Task<IResult> BatchReassignLawfirmLegacy(
        LegacyBatchReassignRequest request,
        ICaseService caseService,
        ILienService lienService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (string.IsNullOrWhiteSpace(request.contactType) ||
            string.IsNullOrWhiteSpace(request.oldId) ||
            string.IsNullOrWhiteSpace(request.newId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assign cases.",
            });
        }

        try
        {
            switch (request.contactType)
            {
                case "1": // law firm
                {
                    if (!Guid.TryParse(request.oldId, out var oldLawFirmOrgId) ||
                        !Guid.TryParse(request.newId, out var newLawFirmOrgId))
                    {
                        return Results.NotFound(new
                        {
                            isSuccess = false,
                            message = "unable to re-assign cases.",
                        });
                    }

                    const int pageSize = 200;
                    var page = 1;
                    while (true)
                    {
                        var pageResult = await caseService.SearchAsync(
                            tenantId,
                            search: null,
                            status: null,
                            page: page,
                            pageSize: pageSize,
                            orgId: oldLawFirmOrgId,
                            ct);

                        if (pageResult.Items.Count == 0)
                            break;

                        foreach (var item in pageResult.Items)
                        {
                            _ = await caseService.ReassignLawFirmAsync(
                                tenantId,
                                item.Id,
                                newLawFirmOrgId,
                                userId,
                                ct);
                        }

                        if ((page * pageSize) >= pageResult.TotalCount)
                            break;

                        page++;
                    }

                    return Results.Ok(new
                    {
                        isSuccess = true,
                        message = "Successfully Reassigned Cases.",
                    });
                }
                case "2": // medical provider
                {
                    const int pageSize = 200;
                    var page = 1;
                    while (true)
                    {
                        var pageResult = await servicingItemService.SearchAsync(
                            tenantId,
                            search: "LegacyMedicalFacilityInfo",
                            status: null,
                            priority: null,
                            assignedTo: null,
                            caseId: null,
                            lienId: null,
                            page: page,
                            pageSize: pageSize,
                            ct);

                        if (pageResult.Items.Count == 0)
                            break;

                        foreach (var item in pageResult.Items.Where(i =>
                                     string.Equals(i.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal)))
                        {
                            var fields = ParseLegacyNoteFields(item.Notes);
                            var currentMedicalProvider = fields.GetValueOrDefault("medicalProviderId", string.Empty);
                            if (!string.Equals(currentMedicalProvider, request.oldId, StringComparison.Ordinal))
                                continue;

                            fields["medicalProviderId"] = request.newId.Trim();

                            var update = new UpdateServicingItemRequest
                            {
                                TaskType = item.TaskType,
                                Description = item.Description,
                                AssignedTo = string.IsNullOrWhiteSpace(item.AssignedTo) ? "system" : item.AssignedTo,
                                AssignedToUserId = item.AssignedToUserId,
                                Priority = item.Priority,
                                Status = item.Status,
                                CaseId = item.CaseId,
                                LienId = item.LienId,
                                DueDate = item.DueDate,
                                Notes = SerializeLegacyNoteFields(fields),
                                Resolution = item.Resolution,
                            };

                            await servicingItemService.UpdateAsync(tenantId, item.Id, userId, update, ct);
                        }

                        if ((page * pageSize) >= pageResult.TotalCount)
                            break;

                        page++;
                    }

                    return Results.Ok(new
                    {
                        isSuccess = true,
                        message = "Successfully Reassigned Liens.",
                    });
                }
                case "3": // funding company
                {
                    const int pageSize = 200;
                    var page = 1;
                    while (true)
                    {
                        var pageResult = await lienService.SearchAsync(
                            tenantId,
                            search: null,
                            status: null,
                            lienType: null,
                            caseId: null,
                            facilityId: null,
                            page: page,
                            pageSize: pageSize,
                            ct);

                        if (pageResult.Items.Count == 0)
                            break;

                        foreach (var item in pageResult.Items.Where(i =>
                                     string.Equals(i.ExternalReference, request.oldId, StringComparison.Ordinal)))
                        {
                            var update = new UpdateLienRequest
                            {
                                ExternalReference = request.newId.Trim(),
                                LienType = item.LienType,
                                CaseId = item.CaseId,
                                FacilityId = item.FacilityId,
                                OriginalAmount = item.OriginalAmount,
                                Jurisdiction = item.Jurisdiction,
                                IsConfidential = item.IsConfidential,
                                SubjectFirstName = item.SubjectFirstName,
                                SubjectLastName = item.SubjectLastName,
                                IncidentDate = item.IncidentDate,
                                Description = item.Description,
                            };

                            await lienService.UpdateAsync(tenantId, item.Id, userId, update, ct);
                        }

                        if ((page * pageSize) >= pageResult.TotalCount)
                            break;

                        page++;
                    }

                    return Results.Ok(new
                    {
                        isSuccess = true,
                        message = "Successfully Reassigned Fundings.",
                    });
                }
                case "4": // medical facility
                {
                    const int pageSize = 200;
                    var page = 1;
                    while (true)
                    {
                        var pageResult = await servicingItemService.SearchAsync(
                            tenantId,
                            search: "LegacyMedicalFacilityInfo",
                            status: null,
                            priority: null,
                            assignedTo: null,
                            caseId: null,
                            lienId: null,
                            page: page,
                            pageSize: pageSize,
                            ct);

                        if (pageResult.Items.Count == 0)
                            break;

                        foreach (var item in pageResult.Items.Where(i =>
                                     string.Equals(i.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal)))
                        {
                            var fields = ParseLegacyNoteFields(item.Notes);
                            var currentFacilityId = fields.GetValueOrDefault("facilityId", string.Empty);
                            if (!string.Equals(currentFacilityId, request.oldId, StringComparison.Ordinal))
                                continue;

                            fields["facilityId"] = request.newId.Trim();

                            var update = new UpdateServicingItemRequest
                            {
                                TaskType = item.TaskType,
                                Description = item.Description,
                                AssignedTo = string.IsNullOrWhiteSpace(item.AssignedTo) ? "system" : item.AssignedTo,
                                AssignedToUserId = item.AssignedToUserId,
                                Priority = item.Priority,
                                Status = item.Status,
                                CaseId = item.CaseId,
                                LienId = item.LienId,
                                DueDate = item.DueDate,
                                Notes = SerializeLegacyNoteFields(fields),
                                Resolution = item.Resolution,
                            };

                            await servicingItemService.UpdateAsync(tenantId, item.Id, userId, update, ct);
                        }

                        if ((page * pageSize) >= pageResult.TotalCount)
                            break;

                        page++;
                    }

                    return Results.Ok(new
                    {
                        isSuccess = true,
                        message = "Successfully Reassigned Liens.",
                    });
                }
                case "5": // leads
                {
                    const int pageSize = 200;
                    var page = 1;
                    while (true)
                    {
                        var pageResult = await caseService.SearchAsync(
                            tenantId,
                            search: null,
                            status: null,
                            page: page,
                            pageSize: pageSize,
                            orgId: null,
                            ct);

                        if (pageResult.Items.Count == 0)
                            break;

                        foreach (var item in pageResult.Items)
                        {
                            var fields = ParseLegacyNoteFields(item.Notes);
                            var currentLeadId = fields.GetValueOrDefault("leadId", string.Empty);
                            if (!string.Equals(currentLeadId, request.oldId, StringComparison.Ordinal))
                                continue;

                            fields["leadId"] = request.newId.Trim();

                            var update = new UpdateCaseRequest
                            {
                                ClientFirstName = item.ClientFirstName,
                                ClientLastName = item.ClientLastName,
                                ExternalReference = item.ExternalReference,
                                Title = item.Title,
                                ClientDob = item.ClientDob,
                                ClientPhone = item.ClientPhone,
                                ClientEmail = item.ClientEmail,
                                ClientAddress = item.ClientAddress,
                                DateOfIncident = item.DateOfIncident,
                                InsuranceCarrier = item.InsuranceCarrier,
                                PolicyNumber = item.PolicyNumber,
                                ClaimNumber = item.ClaimNumber,
                                Description = item.Description,
                                Notes = SerializeLegacyNoteFields(fields),
                                Status = item.Status,
                                DemandAmount = item.DemandAmount,
                                SettlementAmount = item.SettlementAmount,
                            };

                            await caseService.UpdateAsync(tenantId, item.Id, userId, update, ct);
                        }

                        if ((page * pageSize) >= pageResult.TotalCount)
                            break;

                        page++;
                    }

                    return Results.Ok(new
                    {
                        isSuccess = true,
                        message = "Successfully Reassigned Leads.",
                    });
                }
                default:
                    return Results.NotFound(new
                    {
                        isSuccess = false,
                        message = "unable to re-assign cases.",
                    });
            }
        }
        catch
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "unable to re-assign cases.",
            });
        }
    }

    private static async Task<IResult> GeneratePayoffQuoteLegacy(
        Guid caseId,
        ICaseService caseService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        try
        {
            var existingCase = await caseService.GetByIdAsync(tenantId, caseId, ct);
            if (existingCase is null)
            {
                return Results.NotFound(new
                {
                    isSuccess = false,
                    message = "Error: Unable to retrieve Payoff Quote",
                });
            }

            const int pageSize = 200;
            var page = 1;
            var candidates = new List<ServicingItemResponse>();

            while (true)
            {
                var result = await servicingItemService.SearchAsync(
                    tenantId,
                    search: null,
                    status: null,
                    priority: null,
                    assignedTo: null,
                    caseId: caseId,
                    lienId: null,
                    page: page,
                    pageSize: pageSize,
                    ct);

                if (result.Items.Count == 0)
                    break;

                candidates.AddRange(result.Items);
                if (candidates.Count >= result.TotalCount)
                    break;

                page++;
            }

            var payoffUrl = candidates
                .Where(i => string.Equals(i.TaskType, "LegacyCaseDocument", StringComparison.Ordinal))
                .OrderByDescending(i => i.CreatedAtUtc)
                .Select(i => ParseLegacyNoteFields(i.Notes))
                .Where(fields =>
                {
                    var typeId = fields.GetValueOrDefault("typeId", string.Empty);
                    if (string.IsNullOrWhiteSpace(typeId))
                        typeId = fields.GetValueOrDefault("docTypeId", string.Empty);
                    if (string.IsNullOrWhiteSpace(typeId))
                        typeId = fields.GetValueOrDefault("documentTypeId", string.Empty);

                    var category = fields.GetValueOrDefault("category", string.Empty);
                    return string.Equals(typeId, "14", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(category, "PayoffStatement", StringComparison.OrdinalIgnoreCase);
                })
                .Select(fields =>
                {
                    var url = fields.GetValueOrDefault("url", string.Empty);
                    if (string.IsNullOrWhiteSpace(url))
                        url = fields.GetValueOrDefault("documentUrl", string.Empty);
                    return url;
                })
                .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));

            if (!string.IsNullOrWhiteSpace(payoffUrl))
            {
                return Results.Ok(new
                {
                    isSuccess = true,
                    message = "Successfully retrieved Payoff Quote",
                    url = payoffUrl,
                });
            }

            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: Unable to retrieve Payoff Quote",
            });
        }
        catch
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: Unable to retrieve Payoff Quote",
            });
        }
    }

    private static async Task<IResult> GetDashboardLegacy(
        ICaseService caseService,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        const int pageSize = 200;

        var cases = new List<CaseResponse>();
        var casePage = 1;
        while (true)
        {
            var chunk = await caseService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                page: casePage,
                pageSize: pageSize,
                orgId: null,
                ct: ct);

            if (chunk.Items.Count == 0)
                break;

            cases.AddRange(chunk.Items);
            if (cases.Count >= chunk.TotalCount)
                break;

            casePage++;
        }

        var liens = new List<LienResponse>();
        var lienPage = 1;
        while (true)
        {
            var chunk = await lienService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                lienType: null,
                caseId: null,
                facilityId: null,
                page: lienPage,
                pageSize: pageSize,
                ct: ct);

            if (chunk.Items.Count == 0)
                break;

            liens.AddRange(chunk.Items);
            if (liens.Count >= chunk.TotalCount)
                break;

            lienPage++;
        }

        if (cases.Count == 0 && liens.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No dashboard data found.",
            });
        }

        var caseStatus = cases
            .GroupBy(c => string.IsNullOrWhiteSpace(c.Status) ? "Unknown" : c.Status)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { label = g.Key, value = g.Count() })
            .ToList();

        var lienStatus = liens
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Status) ? "Unknown" : l.Status)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { label = g.Key, value = g.Count() })
            .ToList();

        var data = new
        {
            totalCases = cases.Count,
            totalActiveCases = cases.Count(c => !string.Equals(c.Status, CaseStatus.Closed, StringComparison.OrdinalIgnoreCase)),
            totalLiens = liens.Count,
            totalLienValue = liens.Sum(l => (double)l.OriginalAmount),
            caseStatus,
            lienStatus,
        };

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Dashboard data retrieved successfully.",
            data,
        });
    }

    private static async Task<IResult> GenerateCaseCsvLegacy(
        LegacyGenerateCaseCsvRequest request,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        try
        {
            Guid? lawFirmOrgId = null;
            if (!string.IsNullOrWhiteSpace(request.lawFirmId))
            {
                if (!Guid.TryParse(request.lawFirmId, out var parsedLawFirmId))
                {
                    return Results.NotFound(new
                    {
                        isSuccess = false,
                        message = "No data generated.",
                        data = (object?)null,
                    });
                }

                lawFirmOrgId = parsedLawFirmId;
            }

            const int pageSize = 200;
            var page = 1;
            var cases = new List<CaseResponse>();

            while (true)
            {
                var result = await caseService.SearchAsync(
                    tenantId,
                    search: null,
                    status: string.IsNullOrWhiteSpace(request.statusId) ? null : request.statusId,
                    page: page,
                    pageSize: pageSize,
                    orgId: lawFirmOrgId,
                    ct: ct);

                if (result.Items.Count == 0)
                    break;

                cases.AddRange(result.Items);
                if (cases.Count >= result.TotalCount)
                    break;

                page++;
            }

            var filtered = cases
                .Where(c => string.IsNullOrWhiteSpace(request.caseId) ||
                            string.Equals(c.CaseNumber, request.caseId, StringComparison.OrdinalIgnoreCase))
                .Where(c =>
                {
                    if (string.IsNullOrWhiteSpace(request.accidentTypeId))
                        return true;

                    var fields = ParseLegacyNoteFields(c.Notes);
                    return string.Equals(
                        fields.GetValueOrDefault("accidentTypeId", string.Empty),
                        request.accidentTypeId,
                        StringComparison.OrdinalIgnoreCase);
                })
                .Where(c =>
                {
                    if (string.IsNullOrWhiteSpace(request.caseManagerId))
                        return true;

                    var fields = ParseLegacyNoteFields(c.Notes);
                    return string.Equals(
                        fields.GetValueOrDefault("caseManagerId", string.Empty),
                        request.caseManagerId,
                        StringComparison.OrdinalIgnoreCase);
                })
                .OrderByDescending(c => c.CaseNumber, StringComparer.Ordinal)
                .ToList();

            if (filtered.Count == 0)
            {
                return Results.NotFound(new
                {
                    isSuccess = false,
                    message = "No cases found.",
                    data = (object?)null,
                });
            }

            var csvBytes = BuildLegacyCaseCsv(filtered);
            if (csvBytes.Length == 0)
            {
                return Results.NotFound(new
                {
                    isSuccess = false,
                    message = "No data generated.",
                    data = (object?)null,
                });
            }

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var pacificNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            var filename = $"case_{pacificNow:yyyyMMddHHmmss}.csv";
            var exportItem = new
            {
                base64 = Convert.ToBase64String(csvBytes),
                filename,
                export_format = "csv",
            };

            return Results.Ok(new
            {
                isSuccess = true,
                message = "CSV generated successfully.",
                data = new object[] { exportItem },
            });
        }
        catch (Exception ex)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = $"Error generating CSV: {ex.Message}",
                data = (object?)null,
            });
        }
    }

    private static byte[] BuildLegacyCaseCsv(List<CaseResponse> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CaseCode,FirstName,LastName,DateOfBirth,Address,City,State,ZipCode,IsServicing,IsUccFiled,IsBulk,AccidentType,AccidentState,DateOfLoss,LawFirm,CaseManager,Note,Created,CreateBy,Updated,UpdateBy,Status,CurrentStatus,CurrentMedicalStatus,CurrentAttributes,Email,Phone,Gender,SSN,Summary,ToGeneratePdf,SwitchedDate");

        foreach (var item in items)
        {
            var address = SplitLegacyAddress(item.ClientAddress);
            var fields = ParseLegacyNoteFields(item.Notes);
            var row = string.Join(",", new[]
            {
                EscapeLegacyCsv(item.CaseNumber),
                EscapeLegacyCsv(item.ClientFirstName),
                EscapeLegacyCsv(item.ClientLastName),
                EscapeLegacyCsv(FormatLegacyDate(item.ClientDob)),
                EscapeLegacyCsv(address.Address),
                EscapeLegacyCsv(address.City),
                EscapeLegacyCsv(address.State),
                EscapeLegacyCsv(address.Zipcode),
                EscapeLegacyCsv(fields.GetValueOrDefault("isServicing", string.Empty)),
                EscapeLegacyCsv(fields.GetValueOrDefault("isUccFiled", string.Empty)),
                EscapeLegacyCsv(fields.GetValueOrDefault("isBulk", string.Empty)),
                EscapeLegacyCsv(fields.GetValueOrDefault("accidentType", string.Empty)),
                EscapeLegacyCsv(fields.GetValueOrDefault("accidentState", string.Empty)),
                EscapeLegacyCsv(FormatLegacyDate(item.DateOfIncident)),
                EscapeLegacyCsv(fields.GetValueOrDefault("lawFirm", string.Empty)),
                EscapeLegacyCsv(fields.GetValueOrDefault("caseManager", string.Empty)),
                EscapeLegacyCsv(item.Notes ?? string.Empty),
                EscapeLegacyCsv(FormatLegacyTimestamp(item.CreatedAtUtc)),
                EscapeLegacyCsv(string.Empty),
                EscapeLegacyCsv(FormatLegacyTimestamp(item.UpdatedAtUtc)),
                EscapeLegacyCsv(string.Empty),
                EscapeLegacyCsv(item.Status),
                EscapeLegacyCsv(item.Status),
                EscapeLegacyCsv(fields.GetValueOrDefault("currentMedicalStatus", string.Empty)),
                EscapeLegacyCsv(fields.GetValueOrDefault("currentAttributes", string.Empty)),
                EscapeLegacyCsv(item.ClientEmail ?? string.Empty),
                EscapeLegacyCsv(item.ClientPhone ?? string.Empty),
                EscapeLegacyCsv(fields.GetValueOrDefault("gender", string.Empty)),
                EscapeLegacyCsv(fields.GetValueOrDefault("ssn", string.Empty)),
                EscapeLegacyCsv(item.Description ?? string.Empty),
                EscapeLegacyCsv(fields.GetValueOrDefault("toGeneratePdf", string.Empty)),
                EscapeLegacyCsv(fields.GetValueOrDefault("switchedDate", string.Empty)),
            });

            sb.AppendLine(row);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static async Task<IResult> GenerateLiensCsvLegacy(
        LegacyGenerateLiensCsvRequest request,
        ILienService lienService,
        ICaseService caseService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        try
        {
            var caseIdFilter = ParseGuidCsvValues(request.caseId);
            var lienIdFilter = ParseGuidCsvValues(request.liensId);
            var facilityIdFilter = ParseGuidCsvValues(request.medicalFacilityId);
            var lawFirmFilter = ParseGuidCsvValues(request.lawFirmId);
            var caseManagerFilter = (request.caseManagerId ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var lienStatusFilter = (request.lienStatusId ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var allLiens = new List<LienResponse>();
            const int pageSize = 200;
            var page = 1;

            while (true)
            {
                var seededCaseId = caseIdFilter.Count == 1 ? caseIdFilter.First() : (Guid?)null;
                var seededFacilityId = facilityIdFilter.Count == 1 ? facilityIdFilter.First() : (Guid?)null;

                var result = await lienService.SearchAsync(
                    tenantId,
                    search: null,
                    status: null,
                    lienType: null,
                    caseId: seededCaseId,
                    facilityId: seededFacilityId,
                    page: page,
                    pageSize: pageSize,
                    ct);

                if (result.Items.Count == 0)
                    break;

                allLiens.AddRange(result.Items);
                if (allLiens.Count >= result.TotalCount)
                    break;

                page++;
            }

            var lawFirmCaseIds = new HashSet<Guid>();
            if (lawFirmFilter.Count > 0)
            {
                foreach (var orgId in lawFirmFilter)
                {
                    var casePage = 1;
                    while (true)
                    {
                        var result = await caseService.SearchAsync(
                            tenantId,
                            search: null,
                            status: null,
                            page: casePage,
                            pageSize: pageSize,
                            orgId: orgId,
                            ct: ct);

                        if (result.Items.Count == 0)
                            break;

                        foreach (var item in result.Items)
                            lawFirmCaseIds.Add(item.Id);

                        if (lawFirmCaseIds.Count >= result.TotalCount)
                            break;

                        casePage++;
                    }
                }
            }

            var filteredLiens = allLiens
                .Where(l => caseIdFilter.Count == 0 || (l.CaseId.HasValue && caseIdFilter.Contains(l.CaseId.Value)))
                .Where(l => lienIdFilter.Count == 0 || lienIdFilter.Contains(l.Id))
                .Where(l => facilityIdFilter.Count == 0 || (l.FacilityId.HasValue && facilityIdFilter.Contains(l.FacilityId.Value)))
                .Where(l => lawFirmCaseIds.Count == 0 || (l.CaseId.HasValue && lawFirmCaseIds.Contains(l.CaseId.Value)))
                .Where(l => lienStatusFilter.Count == 0 || lienStatusFilter.Contains(l.Status))
                .Where(l => MatchesLegacyPurchaseDateFilter(l.IncidentDate, request.purchaseDate))
                .OrderByDescending(l => l.CreatedAtUtc)
                .ToList();

            var rows = new List<LegacyLiensCsvRow>();
            foreach (var lien in filteredLiens)
            {
                CaseResponse? caseInfo = null;
                Dictionary<string, string> caseFields;
                if (lien.CaseId.HasValue)
                {
                    caseInfo = await caseService.GetByIdAsync(tenantId, lien.CaseId.Value, ct);
                    if (caseInfo is null)
                        continue;

                    caseFields = ParseLegacyNoteFields(caseInfo.Notes);
                }
                else
                {
                    caseFields = new Dictionary<string, string>(StringComparer.Ordinal);
                }

                if (caseManagerFilter.Count > 0)
                {
                    var caseManagerId = caseFields.GetValueOrDefault("caseManagerId", string.Empty);
                    if (!caseManagerFilter.Contains(caseManagerId))
                        continue;
                }

                decimal totalPurchase = 0m;
                decimal totalBilling = 0m;
                if (lien.Id != Guid.Empty)
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

                    foreach (var item in codeResults.Items.Where(i =>
                                 string.Equals(i.TaskType, "LegacyMedicalCode", StringComparison.Ordinal)))
                    {
                        var codeFields = ParseLegacyNoteFields(item.Notes);
                        if (decimal.TryParse(codeFields.GetValueOrDefault("purchaseAmount", string.Empty), NumberStyles.Any, CultureInfo.InvariantCulture, out var purchase))
                            totalPurchase += purchase;
                        if (decimal.TryParse(codeFields.GetValueOrDefault("billingAmount", string.Empty), NumberStyles.Any, CultureInfo.InvariantCulture, out var billing))
                            totalBilling += billing;
                    }
                }

                var facilityFields = new Dictionary<string, string>(StringComparer.Ordinal);
                if (lien.Id != Guid.Empty)
                {
                    var infoResults = await servicingItemService.SearchAsync(
                        tenantId,
                        search: "LegacyMedicalFacilityInfo",
                        status: null,
                        priority: null,
                        assignedTo: null,
                        caseId: null,
                        lienId: lien.Id,
                        page: 1,
                        pageSize: 50,
                        ct);

                    var infoItem = infoResults.Items.FirstOrDefault(i =>
                        string.Equals(i.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal));
                    if (infoItem is not null)
                        facilityFields = ParseLegacyNoteFields(infoItem.Notes);
                }

                var plainTiffName = caseInfo is null
                    ? string.Empty
                    : $"{caseInfo.ClientFirstName} {caseInfo.ClientLastName}".Trim();

                var closedDate = LienStatus.Terminal.Contains(lien.Status)
                    ? FormatLegacyTimestamp(lien.UpdatedAtUtc)
                    : string.Empty;

                rows.Add(new LegacyLiensCsvRow
                {
                    CaseCode = caseInfo?.CaseNumber ?? string.Empty,
                    LiensCode = lien.LienNumber,
                    Status = lien.Status,
                    PurchaseDate = FormatLegacyDate(lien.IncidentDate),
                    InitialServiceDate = facilityFields.GetValueOrDefault("initialServiceDate", string.Empty),
                    EndServiceDate = facilityFields.GetValueOrDefault("endServiceDate", string.Empty),
                    Note = lien.Description ?? string.Empty,
                    FacilityEmail = facilityFields.GetValueOrDefault("email", string.Empty),
                    FacilityPhone = facilityFields.GetValueOrDefault("phone", string.Empty),
                    TotalPurchase = totalPurchase.ToString("#,##0.00", CultureInfo.InvariantCulture),
                    TotalBilling = totalBilling.ToString("#,##0.00", CultureInfo.InvariantCulture),
                    LawFirm = caseFields.GetValueOrDefault("lawFirm", string.Empty),
                    CaseManager = caseFields.GetValueOrDefault("caseManager", string.Empty),
                    FacilityName = facilityFields.GetValueOrDefault("facilityName", string.Empty),
                    FacilityContactName = facilityFields.GetValueOrDefault("facilityContactPerson", string.Empty),
                    MedicalProvider = facilityFields.GetValueOrDefault("medicalProvider", string.Empty),
                    PlainTiffName = plainTiffName,
                    ClosedDate = closedDate,
                });
            }

            if (rows.Count == 0)
            {
                return Results.NotFound(new
                {
                    isSuccess = false,
                    message = "No liens found. ",
                    data = (object?)null,
                });
            }

            var csvBytes = BuildLegacyLiensCsv(rows);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var pacificNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            var filename = $"liens_{pacificNow:yyyyMMddHHmmss}.csv";
            var exportItem = new
            {
                base64 = Convert.ToBase64String(csvBytes),
                filename,
                export_format = "csv",
            };

            return Results.Ok(new
            {
                isSuccess = true,
                message = "CSV generated successfully.",
                data = new object[] { exportItem },
            });
        }
        catch (Exception ex)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = $"Error generating CSV:  {ex.Message}",
                data = (object?)null,
            });
        }
    }

    private static byte[] BuildLegacyLiensCsv(List<LegacyLiensCsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CaseCode,LiensCode,Status,PurchaseDate,InitialServiceDate,EndServiceDate,Note,FacilityEmail,FacilityPhone,TotalPurchase,TotalBilling,LawFirm,CaseManager,FacilityName,FacilityContactName,MedicalProvider,PlainTiffName,ClosedDate");

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                EscapeLegacyCsv(row.CaseCode),
                EscapeLegacyCsv(row.LiensCode),
                EscapeLegacyCsv(row.Status),
                EscapeLegacyCsv(row.PurchaseDate),
                EscapeLegacyCsv(row.InitialServiceDate),
                EscapeLegacyCsv(row.EndServiceDate),
                EscapeLegacyCsv(row.Note),
                EscapeLegacyCsv(row.FacilityEmail),
                EscapeLegacyCsv(row.FacilityPhone),
                EscapeLegacyCsv(row.TotalPurchase),
                EscapeLegacyCsv(row.TotalBilling),
                EscapeLegacyCsv(row.LawFirm),
                EscapeLegacyCsv(row.CaseManager),
                EscapeLegacyCsv(row.FacilityName),
                EscapeLegacyCsv(row.FacilityContactName),
                EscapeLegacyCsv(row.MedicalProvider),
                EscapeLegacyCsv(row.PlainTiffName),
                EscapeLegacyCsv(row.ClosedDate),
            }));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static HashSet<Guid> ParseGuidCsvValues(string? raw)
    {
        var set = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(raw))
            return set;

        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(token, out var id))
                set.Add(id);
        }

        return set;
    }

    private static bool MatchesLegacyPurchaseDateFilter(DateOnly? value, string? rawFilter)
    {
        if (string.IsNullOrWhiteSpace(rawFilter))
            return true;
        if (!value.HasValue)
            return false;

        if (rawFilter.Contains('-', StringComparison.Ordinal))
        {
            var range = rawFilter
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (range.Length == 2 &&
                DateOnly.TryParseExact(range[0], "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) &&
                DateOnly.TryParseExact(range[1], "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            {
                return value.Value >= start && value.Value <= end;
            }
        }

        if (DateOnly.TryParseExact(rawFilter, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return value.Value == exact;

        return true;
    }

    private static string EscapeLegacyCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
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

    internal static Guid RequireTenantId(ICurrentRequestContext ctx)
    {
        return ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");
    }

    internal static Guid RequireUserId(ICurrentRequestContext ctx)
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
        var result = await caseService.SearchAsync(tenantId, search, status, page, pageSize, ct: ct);
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
        var caseMetadata = ParseLegacyNoteFields(item.Notes);

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
            caseManagerId = caseMetadata.GetValueOrDefault("caseManagerId", string.Empty),
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

    private static async Task<IResult> GetCaseByLawFirmIdLegacy(
        string lawFirmId,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        bool isTotal = false,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        if (!Guid.TryParse(lawFirmId, out var lawFirmOrgId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found for the specified law firm.",
            });
        }

        var page = 1;
        var pageSize = 100;
        var data = new List<CaseResponse>();

        while (true)
        {
            var result = await caseService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                page: page,
                pageSize: pageSize,
                orgId: lawFirmOrgId,
                ct);

            if (result.Items.Count == 0)
                break;

            data.AddRange(result.Items);

            if (!isTotal || data.Count >= result.TotalCount)
                break;

            page++;
        }

        if (data.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found for the specified law firm.",
            });
        }

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Case list retrieved successfully.",
            data,
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

    private static async Task<IResult> GetLawFirmV3Legacy(
        LegacyLawFirmV3Request req,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        if (!Guid.TryParse(req.LawFirmId, out var lawFirmOrgId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var page = req.Page < 1 ? 1 : req.Page;
        var limit = req.Limit < 1 ? 10 : req.Limit;

        var paged = await caseService.SearchAsync(
            tenantId,
            req.Keyword,
            status: null,
            page,
            limit,
            orgId: lawFirmOrgId,
            ct);

        if (paged.TotalCount == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var allCases = new List<CaseResponse>();
        var totalPages = (int)Math.Ceiling((double)paged.TotalCount / limit);

        for (var currentPage = 1; currentPage <= totalPages; currentPage++)
        {
            var chunk = await caseService.SearchAsync(
                tenantId,
                req.Keyword,
                status: null,
                page: currentPage,
                pageSize: limit,
                orgId: lawFirmOrgId,
                ct);

            allCases.AddRange(chunk.Items);
        }

        var totalCount = paged.TotalCount;
        var totalCases = totalCount;
        var totalActiveCases = allCases.Count(c => !string.Equals(c.Status, CaseStatus.Closed, StringComparison.Ordinal));
        var totalValue = allCases.Sum(c => (double)(c.SettlementAmount ?? c.DemandAmount ?? 0m));

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Case list retrieved successfully.",
            data = paged.Items,
            totalCount,
            totalCases,
            totalActiveCases,
            totalValue,
        });
    }

    private static async Task<IResult> GetLiensByMedicalIdV3Legacy(
        LegacyMedicalLiensV3Request req,
        ICaseService caseService,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        if (!Guid.TryParse(req.MedicalId, out var medicalId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var allLiens = new List<LienResponse>();
        var lienPage = 1;
        const int lienPageSize = 200;

        while (true)
        {
            var liens = await lienService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                lienType: null,
                caseId: null,
                facilityId: medicalId,
                page: lienPage,
                pageSize: lienPageSize,
                ct);

            if (liens.Items.Count == 0)
                break;

            allLiens.AddRange(liens.Items);

            if (allLiens.Count >= liens.TotalCount)
                break;

            lienPage++;
        }

        var caseIds = allLiens
            .Select(l => l.CaseId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (caseIds.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var allCases = new List<CaseResponse>();
        foreach (var caseId in caseIds)
        {
            var item = await caseService.GetByIdAsync(tenantId, caseId, ct);
            if (item is not null)
                allCases.Add(item);
        }

        IEnumerable<CaseResponse> query = allCases;
        if (!string.IsNullOrWhiteSpace(req.Keyword))
        {
            var keyword = req.Keyword.Trim();
            query = query.Where(c =>
                c.CaseNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                c.ClientFirstName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                c.ClientLastName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(c.ClientDisplayName) && c.ClientDisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        var filtered = query.ToList();
        if (filtered.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var page = req.Page < 1 ? 1 : req.Page;
        var limit = req.Limit < 1 ? 10 : req.Limit;

        var paged = filtered
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        var totalCount = filtered.Count;
        var totalCases = totalCount;
        var totalActiveCases = filtered.Count(c => !string.Equals(c.Status, CaseStatus.Closed, StringComparison.Ordinal));
        var totalValue = filtered.Sum(c => (double)(c.SettlementAmount ?? c.DemandAmount ?? 0m));

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Case list retrieved successfully.",
            data = paged,
            totalCount,
            totalCases,
            totalActiveCases,
            totalValue,
        });
    }

    private static async Task<IResult> GetLiensByFundingCompanyIdV3Legacy(
        LegacyFundingCompanyLiensV3Request req,
        ICaseService caseService,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        var fundingCompanyId = req.FundingCompanyId?.Trim();
        if (string.IsNullOrWhiteSpace(fundingCompanyId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var allLiens = new List<LienResponse>();
        var lienPage = 1;
        const int lienPageSize = 200;

        while (true)
        {
            var liens = await lienService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                lienType: null,
                caseId: null,
                facilityId: null,
                page: lienPage,
                pageSize: lienPageSize,
                ct);

            if (liens.Items.Count == 0)
                break;

            allLiens.AddRange(liens.Items);

            if (allLiens.Count >= liens.TotalCount)
                break;

            lienPage++;
        }

        var caseIds = allLiens
            .Where(l => string.Equals(l.ExternalReference, fundingCompanyId, StringComparison.OrdinalIgnoreCase))
            .Select(l => l.CaseId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (caseIds.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var allCases = new List<CaseResponse>();
        foreach (var caseId in caseIds)
        {
            var item = await caseService.GetByIdAsync(tenantId, caseId, ct);
            if (item is not null)
                allCases.Add(item);
        }

        IEnumerable<CaseResponse> query = allCases;
        if (!string.IsNullOrWhiteSpace(req.Keyword))
        {
            var keyword = req.Keyword.Trim();
            query = query.Where(c =>
                c.CaseNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                c.ClientFirstName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                c.ClientLastName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(c.ClientDisplayName) && c.ClientDisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        var filtered = query.ToList();
        if (filtered.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var page = req.Page < 1 ? 1 : req.Page;
        var limit = req.Limit < 1 ? 10 : req.Limit;

        var paged = filtered
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        var totalCount = filtered.Count;
        var totalCases = totalCount;
        var totalActiveCases = filtered.Count(c => !string.Equals(c.Status, CaseStatus.Closed, StringComparison.Ordinal));
        var totalValue = filtered.Sum(c => (double)(c.SettlementAmount ?? c.DemandAmount ?? 0m));

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Case list retrieved successfully.",
            data = paged,
            totalCount,
            totalCases,
            totalActiveCases,
            totalValue,
        });
    }

    private static async Task<IResult> GetLiensByMedicalFacilityIdV3Legacy(
        LegacyFacilityLiensV3Request req,
        ICaseService caseService,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        if (!Guid.TryParse(req.FacilityId, out var facilityId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var allLiens = new List<LienResponse>();
        var lienPage = 1;
        const int lienPageSize = 200;

        while (true)
        {
            var liens = await lienService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                lienType: null,
                caseId: null,
                facilityId: facilityId,
                page: lienPage,
                pageSize: lienPageSize,
                ct);

            if (liens.Items.Count == 0)
                break;

            allLiens.AddRange(liens.Items);

            if (allLiens.Count >= liens.TotalCount)
                break;

            lienPage++;
        }

        var caseIds = allLiens
            .Select(l => l.CaseId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (caseIds.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var allCases = new List<CaseResponse>();
        foreach (var caseId in caseIds)
        {
            var item = await caseService.GetByIdAsync(tenantId, caseId, ct);
            if (item is not null)
                allCases.Add(item);
        }

        IEnumerable<CaseResponse> query = allCases;
        if (!string.IsNullOrWhiteSpace(req.Keyword))
        {
            var keyword = req.Keyword.Trim();
            query = query.Where(c =>
                c.CaseNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                c.ClientFirstName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                c.ClientLastName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(c.ClientDisplayName) && c.ClientDisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        var filtered = query.ToList();
        if (filtered.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var page = req.Page < 1 ? 1 : req.Page;
        var limit = req.Limit < 1 ? 10 : req.Limit;

        var paged = filtered
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        var totalCount = filtered.Count;
        var totalCases = totalCount;
        var totalActiveCases = filtered.Count(c => !string.Equals(c.Status, CaseStatus.Closed, StringComparison.Ordinal));
        var totalValue = filtered.Sum(c => (double)(c.SettlementAmount ?? c.DemandAmount ?? 0m));

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Case list retrieved successfully.",
            data = paged,
            totalCount,
            totalCases,
            totalActiveCases,
            totalValue,
        });
    }

    private static async Task<IResult> GetLeadV3Legacy(
        LegacyLeadCaseV3Request req,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        var leadId = req.LeadId?.Trim();
        if (string.IsNullOrWhiteSpace(leadId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var allCases = new List<CaseResponse>();
        var page = 1;
        const int fetchPageSize = 200;

        while (true)
        {
            var chunk = await caseService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                page: page,
                pageSize: fetchPageSize,
                orgId: null,
                ct);

            if (chunk.Items.Count == 0)
                break;

            allCases.AddRange(chunk.Items);

            if (allCases.Count >= chunk.TotalCount)
                break;

            page++;
        }

        var filteredByLead = allCases
            .Where(c =>
            {
                var fields = ParseLegacyNoteFields(c.Notes);
                var value = fields.GetValueOrDefault("leadId", string.Empty);
                return string.Equals(value, leadId, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        IEnumerable<CaseResponse> query = filteredByLead;
        if (!string.IsNullOrWhiteSpace(req.Keyword))
        {
            var keyword = req.Keyword.Trim();
            query = query.Where(c =>
                c.CaseNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                c.ClientFirstName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                c.ClientLastName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(c.ClientDisplayName) &&
                 c.ClientDisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        var filtered = query.ToList();
        if (filtered.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No cases found.",
            });
        }

        var requestPage = req.Page < 1 ? 1 : req.Page;
        var requestLimit = req.Limit < 1 ? 10 : req.Limit;

        var paged = filtered
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((requestPage - 1) * requestLimit)
            .Take(requestLimit)
            .ToList();

        var totalCount = filtered.Count;
        var totalCases = totalCount;
        var totalActiveCases = filtered.Count(c => !string.Equals(c.Status, CaseStatus.Closed, StringComparison.Ordinal));
        var totalValue = filtered.Sum(c => (double)(c.SettlementAmount ?? c.DemandAmount ?? 0m));

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Case list retrieved successfully.",
            data = paged,
            totalCount,
            totalCases,
            totalActiveCases,
            totalValue,
        });
    }

    private static async Task<IResult> GetCaseUpdatesV3Legacy(
        LegacyCaseUpdatesV3Request req,
        ILienCaseNoteService caseNoteService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        if (!Guid.TryParse(req.CaseId, out var caseId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No case updates found.",
            });
        }

        var notes = await caseNoteService.GetNotesAsync(tenantId, caseId, ct);
        var ordered = notes
            .OrderByDescending(n => n.UpdatedAtUtc ?? n.CreatedAtUtc)
            .ToList();

        if (ordered.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No case updates found.",
            });
        }

        var page = req.Page < 1 ? 1 : req.Page;
        var limit = req.Limit < 1 ? 10 : req.Limit;

        var data = ordered
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(n => new
            {
                id = n.Id.ToString(),
                caseId = n.CaseId.ToString(),
                note = n.Content,
                category = n.Category,
                isPinned = n.IsPinned,
                isEdited = n.IsEdited,
                created = FormatLegacyTimestamp(n.CreatedAtUtc),
                createdBy = n.CreatedByName,
                updated = n.UpdatedAtUtc.HasValue ? FormatLegacyTimestamp(n.UpdatedAtUtc.Value) : string.Empty,
                updatedBy = n.CreatedByName,
            })
            .ToList();

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Case updates retrieved successfully.",
            data,
            totalCount = ordered.Count,
            page,
            limit,
        });
    }

    private static async Task<IResult> GetLiensUpdatesV3Legacy(
        LegacyLiensUpdatesV3Request req,
        ILienService lienService,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        if (!Guid.TryParse(req.CaseId, out var caseId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No liens updates found.",
            });
        }

        const int fetchPageSize = 200;

        var liens = new List<LienResponse>();
        var lienPage = 1;
        while (true)
        {
            var chunk = await lienService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                lienType: null,
                caseId: caseId,
                facilityId: null,
                page: lienPage,
                pageSize: fetchPageSize,
                ct);

            if (chunk.Items.Count == 0)
                break;

            liens.AddRange(chunk.Items);

            if (liens.Count >= chunk.TotalCount)
                break;

            lienPage++;
        }

        var servicingItems = new List<ServicingItemResponse>();
        var servicingPage = 1;
        while (true)
        {
            var chunk = await servicingItemService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                priority: null,
                assignedTo: null,
                caseId: caseId,
                lienId: null,
                page: servicingPage,
                pageSize: fetchPageSize,
                ct);

            if (chunk.Items.Count == 0)
                break;

            servicingItems.AddRange(chunk.Items);

            if (servicingItems.Count >= chunk.TotalCount)
                break;

            servicingPage++;
        }

        var combined = liens
            .Select(l => new
            {
                id = l.Id.ToString(),
                caseId = caseId.ToString(),
                lienId = l.Id.ToString(),
                action = "LienStatus",
                description = string.IsNullOrWhiteSpace(l.Status)
                    ? "Lien update"
                    : $"Lien status updated to {l.Status}.",
                updatedBy = string.Empty,
                timestamp = FormatLegacyTimestamp(l.UpdatedAtUtc),
                sortAt = l.UpdatedAtUtc,
            })
            .Concat(
                servicingItems
                    .Where(i => i.LienId.HasValue)
                    .Select(i => new
                    {
                        id = i.Id.ToString(),
                        caseId = i.CaseId?.ToString() ?? caseId.ToString(),
                        lienId = i.LienId!.Value.ToString(),
                        action = i.TaskType,
                        description = string.IsNullOrWhiteSpace(i.Resolution) ? i.Description : i.Resolution,
                        updatedBy = i.AssignedTo,
                        timestamp = FormatLegacyTimestamp(i.UpdatedAtUtc),
                        sortAt = i.UpdatedAtUtc,
                    }))
            .OrderByDescending(i => i.sortAt)
            .ToList();

        if (combined.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No liens updates found.",
            });
        }

        var page = req.Page < 1 ? 1 : req.Page;
        var limit = req.Limit < 1 ? 10 : req.Limit;

        var data = combined
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(i => new
            {
                i.id,
                i.caseId,
                i.lienId,
                i.action,
                i.description,
                i.updatedBy,
                i.timestamp,
            })
            .ToList();

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Liens updates retrieved successfully.",
            data,
            totalCount = combined.Count,
            page,
            limit,
        });
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
        Guid? lawFirmOrgId = null;

        if (!string.IsNullOrWhiteSpace(filter.lawFirmId))
        {
            if (!Guid.TryParse(filter.lawFirmId, out var parsedLawFirmId))
            {
                return Results.NotFound(new
                {
                    isSuccess = false,
                    message = "No cases found.",
                });
            }

            lawFirmOrgId = parsedLawFirmId;
        }

        var result = await caseService.SearchV3Async(
            tenantId,
            filter.keyword,
            filter.statusId,
            page,
            limit,
            filter.sortBy,
            filter.sortDirection,
            lawFirmOrgId,
            filter.accidentTypeId,
            filter.caseManagerId,
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

    // ── Partial-update handlers ───────────────────────────────────────────────

    private sealed class PersonalUpdateRequest
    {
        public Guid    CaseId        { get; init; }
        public string  FirstName     { get; init; } = string.Empty;
        public string  LastName      { get; init; } = string.Empty;
        public string? Dob           { get; init; }
        public string? Phone         { get; init; }
        public string? Email         { get; init; }
        public string? Address       { get; init; }
        public string? City          { get; init; }
        public string? State         { get; init; }
        public string? Zipcode       { get; init; }
    }

    private sealed class PrimaryUpdateRequest
    {
        public Guid     CaseId      { get; init; }
        public string?  Title       { get; init; }
        public string?  Status      { get; init; }
        public string?  DateOfLoss  { get; init; }
        public string?  InsuranceCarrier { get; init; }
        public string?  PolicyNumber    { get; init; }
        public string?  ClaimNumber     { get; init; }
    }

    private sealed class CaseDetailsUpdateRequest
    {
        public Guid     CaseId           { get; init; }
        public string?  Description      { get; init; }
        public string?  Notes            { get; init; }
        public decimal? DemandAmount     { get; init; }
        public decimal? SettlementAmount { get; init; }
    }

    private static async Task<IResult> UpdatePersonalInfo(
        PersonalUpdateRequest req,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var existing = await caseService.GetByIdAsync(tenantId, req.CaseId, ct);
        if (existing is null)
            return Results.NotFound(new { isSuccess = false, message = "Case not found." });

        DateOnly? dob = DateOnly.TryParse(req.Dob, out var d) ? d : existing.ClientDob;
        var address = string.IsNullOrWhiteSpace(req.Address)
            ? existing.ClientAddress
            : $"{req.Address}, {req.City}, {req.State} {req.Zipcode}".Trim(',', ' ');

        var request = new UpdateCaseRequest
        {
            ClientFirstName  = req.FirstName,
            ClientLastName   = req.LastName,
            ClientDob        = dob,
            ClientPhone      = req.Phone ?? existing.ClientPhone,
            ClientEmail      = req.Email ?? existing.ClientEmail,
            ClientAddress    = address,
            ExternalReference= existing.ExternalReference,
            Title            = existing.Title,
            DateOfIncident   = existing.DateOfIncident,
            Status           = existing.Status,
            InsuranceCarrier = existing.InsuranceCarrier,
            PolicyNumber     = existing.PolicyNumber,
            ClaimNumber      = existing.ClaimNumber,
            Description      = existing.Description,
            Notes            = existing.Notes,
            DemandAmount     = existing.DemandAmount,
            SettlementAmount = existing.SettlementAmount,
        };
        await caseService.UpdateAsync(tenantId, req.CaseId, userId, request, ct);
        return Results.Ok(new { isSuccess = true, message = "Successfully Updated." });
    }

    private static async Task<IResult> UpdatePrimaryInfo(
        PrimaryUpdateRequest req,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var existing = await caseService.GetByIdAsync(tenantId, req.CaseId, ct);
        if (existing is null)
            return Results.NotFound(new { isSuccess = false, message = "Case not found." });

        DateOnly? dateOfLoss = DateOnly.TryParse(req.DateOfLoss, out var dl) ? dl : existing.DateOfIncident;
        var request = new UpdateCaseRequest
        {
            ClientFirstName  = existing.ClientFirstName,
            ClientLastName   = existing.ClientLastName,
            ClientDob        = existing.ClientDob,
            ClientPhone      = existing.ClientPhone,
            ClientEmail      = existing.ClientEmail,
            ClientAddress    = existing.ClientAddress,
            ExternalReference= existing.ExternalReference,
            Title            = req.Title ?? existing.Title,
            DateOfIncident   = dateOfLoss,
            Status           = req.Status ?? existing.Status,
            InsuranceCarrier = req.InsuranceCarrier ?? existing.InsuranceCarrier,
            PolicyNumber     = req.PolicyNumber ?? existing.PolicyNumber,
            ClaimNumber      = req.ClaimNumber ?? existing.ClaimNumber,
            Description      = existing.Description,
            Notes            = existing.Notes,
            DemandAmount     = existing.DemandAmount,
            SettlementAmount = existing.SettlementAmount,
        };
        await caseService.UpdateAsync(tenantId, req.CaseId, userId, request, ct);
        return Results.Ok(new { isSuccess = true, message = "Successfully Updated." });
    }

    private static async Task<IResult> UpdateCaseDetails(
        CaseDetailsUpdateRequest req,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var existing = await caseService.GetByIdAsync(tenantId, req.CaseId, ct);
        if (existing is null)
            return Results.NotFound(new { isSuccess = false, message = "Case not found." });

        var request = new UpdateCaseRequest
        {
            ClientFirstName  = existing.ClientFirstName,
            ClientLastName   = existing.ClientLastName,
            ClientDob        = existing.ClientDob,
            ClientPhone      = existing.ClientPhone,
            ClientEmail      = existing.ClientEmail,
            ClientAddress    = existing.ClientAddress,
            ExternalReference= existing.ExternalReference,
            Title            = existing.Title,
            DateOfIncident   = existing.DateOfIncident,
            Status           = existing.Status,
            InsuranceCarrier = existing.InsuranceCarrier,
            PolicyNumber     = existing.PolicyNumber,
            ClaimNumber      = existing.ClaimNumber,
            Description      = req.Description ?? existing.Description,
            Notes            = req.Notes ?? existing.Notes,
            DemandAmount     = req.DemandAmount ?? existing.DemandAmount,
            SettlementAmount = req.SettlementAmount ?? existing.SettlementAmount,
        };
        await caseService.UpdateAsync(tenantId, req.CaseId, userId, request, ct);
        return Results.Ok(new { isSuccess = true, message = "Successfully Updated." });
    }

    // ── Linked-entity filter stub ─────────────────────────────────────────────
    // The v2 Case entity does not carry direct FK references to contacts (law
    // firm, medical provider, funding company, case manager).  These routes are
    // stubs that return an empty paginated result until the data model is
    // extended with the appropriate FK columns.
    private static Task<IResult> GetCasesByLinkedEntity(
        ICurrentRequestContext _ctx,
        CancellationToken _ct = default)
    {
        return Task.FromResult<IResult>(Results.Ok(new PaginatedResult<CaseResponse>()));
    }

    // ── Audit log stubs ───────────────────────────────────────────────────────
    private static Task<IResult> GetCaseAuditLog(
        Guid caseId,
        ICurrentRequestContext _ctx,
        CancellationToken _ct = default)
        => Task.FromResult<IResult>(Results.Ok(Array.Empty<object>()));

    private static Task<IResult> GetLiensAuditLog(
        Guid caseId,
        ICurrentRequestContext _ctx,
        CancellationToken _ct = default)
        => Task.FromResult<IResult>(Results.Ok(Array.Empty<object>()));

    // ── Liens list from case context ──────────────────────────────────────────
    private sealed class LiensFilterRequest
    {
        public int    Page    { get; init; } = 1;
        public int    Limit   { get; init; } = 20;
        public string? Status { get; init; }
        public Guid?   CaseId { get; init; }
    }

    private static async Task<IResult> ListLiensByCaseContext(
        LiensFilterRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await lienService.SearchAsync(
            tenantId, null, request.Status, null,
            request.CaseId, null, request.Page, request.Limit, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListLiensByCaseId(
        Guid caseId,
        LiensFilterRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await lienService.SearchAsync(
            tenantId, null, request.Status, null,
            caseId, null, request.Page, request.Limit, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SearchLiensV3(
        LiensFilterRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await lienService.SearchAsync(
            tenantId, null, request.Status, null,
            request.CaseId, null, request.Page, request.Limit, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetLiensDetailsByCaseId(
        Guid caseId,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await lienService.SearchAsync(
            tenantId, null, null, null,
            caseId, null, page: 1, pageSize: 500, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteLien(
        Guid liensId,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        await lienService.DeleteAsync(tenantId, liensId, userId, ct);
        return Results.Ok(new { isSuccess = true, message = "Successfully Deleted." });
    }

    // ── Manual medical codes (stubs) ─────────────────────────────────────────
    // These require a ManualMedicalCode entity (planned for a future migration).
    private sealed class ManualMedicalCodeRequest
    {
        public Guid    LienId   { get; init; }
        public string? Code     { get; init; }
        public string? Name     { get; init; }
        public decimal? Cost    { get; init; }
    }

    private static Task<IResult> CreateManualMedicalCode(
        ManualMedicalCodeRequest _req,
        ICurrentRequestContext _ctx,
        CancellationToken _ct = default)
        => Task.FromResult(Results.StatusCode(501));

    private static Task<IResult> UpdateManualMedicalCode(
        ManualMedicalCodeRequest _req,
        ICurrentRequestContext _ctx,
        CancellationToken _ct = default)
        => Task.FromResult(Results.StatusCode(501));

    // ── Dashboard stubs ───────────────────────────────────────────────────────
    private sealed class ReportFilterRequest
    {
        public int    Page          { get; init; } = 1;
        public int    Limit         { get; init; } = 20;
        public string? FilterType   { get; init; }
        public string? FilterId     { get; init; }
    }

    private static Task<IResult> GetDashboardTaskSummary(ICurrentRequestContext _) =>
        Task.FromResult<IResult>(Results.Ok(new { totalTasks = 0, overdue = 0, dueToday = 0 }));

    private static Task<IResult> GetTotalLienReport(ICurrentRequestContext _) =>
        Task.FromResult<IResult>(Results.Ok(Array.Empty<object>()));
    private static Task<IResult> GetTotalLienReportV3(ReportFilterRequest _, ICurrentRequestContext __) =>
        Task.FromResult<IResult>(Results.Ok(new PaginatedResult<object>()));

    private static Task<IResult> GetTotalCaseReport(ICurrentRequestContext _) =>
        Task.FromResult<IResult>(Results.Ok(Array.Empty<object>()));
    private static Task<IResult> GetTotalCaseReportV3(ReportFilterRequest _, ICurrentRequestContext __) =>
        Task.FromResult<IResult>(Results.Ok(new PaginatedResult<object>()));

    private static Task<IResult> GetLawFirmCaseReport(ICurrentRequestContext _) =>
        Task.FromResult<IResult>(Results.Ok(Array.Empty<object>()));
    private static Task<IResult> GetLawFirmCaseReportV3(ReportFilterRequest _, ICurrentRequestContext __) =>
        Task.FromResult<IResult>(Results.Ok(new PaginatedResult<object>()));

    private static Task<IResult> GetMedicalProviderReport(ICurrentRequestContext _) =>
        Task.FromResult<IResult>(Results.Ok(Array.Empty<object>()));
    private static Task<IResult> GetMedicalProviderReportV3(ReportFilterRequest _, ICurrentRequestContext __) =>
        Task.FromResult<IResult>(Results.Ok(new PaginatedResult<object>()));

    private static Task<IResult> GetLienReportCsv(ICurrentRequestContext _) =>
        Task.FromResult<IResult>(Results.Ok(new { data = string.Empty }));
    private static Task<IResult> GetCaseReportCsv(ICurrentRequestContext _) =>
        Task.FromResult<IResult>(Results.Ok(new { data = string.Empty }));
    private static Task<IResult> GetLawFirmCaseReportCsv(ICurrentRequestContext _) =>
        Task.FromResult<IResult>(Results.Ok(new { data = string.Empty }));
    private static Task<IResult> GetMedicalProviderCaseReportCsv(ICurrentRequestContext _) =>
        Task.FromResult<IResult>(Results.Ok(new { data = string.Empty }));

    // ── CSV import stubs ──────────────────────────────────────────────────────
    private sealed class ImportCsvRequest
    {
        public string? FileContent { get; init; }
        public string? FileName    { get; init; }
    }

    private static Task<IResult> ImportCsv(
        ImportCsvRequest _req,
        ICurrentRequestContext _ctx,
        CancellationToken _ct = default)
        => Task.FromResult(Results.StatusCode(501));

    // ── Document type ─────────────────────────────────────────────────────────
    private sealed class DocumentTypeRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    private static Task<IResult> AddDocumentType(
        DocumentTypeRequest _req,
        ICurrentRequestContext _ctx,
        CancellationToken _ct = default)
        => Task.FromResult(Results.StatusCode(501));

    // ── Global search ─────────────────────────────────────────────────────────
    private sealed class GlobalSearchRequest
    {
        public string? Query { get; init; }
        public int Page      { get; init; } = 1;
        public int Limit     { get; init; } = 20;
    }

    private static async Task<IResult> GlobalSearch(
        GlobalSearchRequest request,
        ICaseService caseService,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var cases = await caseService.SearchAsync(
            tenantId, request.Query, null, request.Page, request.Limit, null, ct);
        var liens = await lienService.SearchAsync(
            tenantId, request.Query, null, null, null, null,
            request.Page, request.Limit, ct);
        return Results.Ok(new { cases, liens });
    }
}
