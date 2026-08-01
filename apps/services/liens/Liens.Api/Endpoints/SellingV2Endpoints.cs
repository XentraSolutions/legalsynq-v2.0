using System.Globalization;
using System.Text.Json;
using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Api.Endpoints;

/// <summary>
/// Lien-first Selling V2 routes. These handlers deliberately scope every seller
/// lookup to tenant and selling organisation; the legacy portfolio routes remain
/// available beside this contract during the frontend migration.
/// </summary>
public static class SellingV2Endpoints
{
    private const string SellingMedicalPricingTaskType = "SellingMedicalPricing";
    private const string SellingDocumentTaskType = "SellingDocumentReference";
    private static readonly HashSet<string> IntakeStatuses =
    [
        SellingLienStatus.Pending,
        SellingLienStatus.Internal,
    ];

    public static void MapSellingV2Endpoints(this WebApplication app)
    {
        var seller = app.MapGroup("/api/liens/selling")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .RequireSellMode();

        seller.MapPost("/liens", CreateLien)
            .RequirePermission(LiensPermissions.LienSaleCreate);
        seller.MapGet("/liens/{lienId:guid}", GetLienDetail)
            .RequirePermission(LiensPermissions.LienSaleRead);
        seller.MapGet("/liens/{lienId:guid}/activity", GetLienActivity)
            .RequirePermission(LiensPermissions.LienSaleRead);
        seller.MapPut("/liens/{lienId:guid}/lien-information", SaveLienInformation)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        seller.MapPut("/liens/{lienId:guid}/case-information", SaveCaseInformation)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        seller.MapPut("/liens/{lienId:guid}/medical-pricing", SaveMedicalPricing)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        seller.MapPut("/liens/{lienId:guid}/documents", SaveDocuments)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        seller.MapPost("/liens/{lienId:guid}/prepare-sale", PrepareSale)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        seller.MapPost("/liens/{lienId:guid}/confirm-sale", ConfirmSale)
            .RequirePermission(LiensPermissions.LienSalePublish);
        seller.MapPost("/liens/{lienId:guid}/withdraw-sale", WithdrawSale)
            .RequirePermission(LiensPermissions.LienSaleWithdraw);
        seller.MapPost("/liens/{lienId:guid}/archive", ArchiveLien)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        seller.MapPost("/liens/{lienId:guid}/buyer-access-links", CreateBuyerAccessLink)
            .RequirePermission(LiensPermissions.LienSalePublish);

        seller.MapGet("/bulk-imports/{importId:guid}", GetBulkImport)
            .RequirePermission(LiensPermissions.LienSaleRead);
        seller.MapGet("/bulk-imports/{importId:guid}/rows", GetBulkImportRows)
            .RequirePermission(LiensPermissions.LienSaleRead);
        seller.MapPost("/bulk-imports/{importId:guid}/validate", ValidateBulkImport)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        seller.MapPost("/bulk-imports/{importId:guid}/confirm", ConfirmBulkImport)
            .RequirePermission(LiensPermissions.LienSaleCreate);
        seller.MapDelete("/bulk-imports/{importId:guid}", CancelBulkImport)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        seller.MapGet("/lookups/funding-companies", GetFundingCompanies)
            .RequirePermission(LiensPermissions.LienSaleRead);
        seller.MapGet("/lookups/funding-company-contacts", GetFundingCompanyContacts)
            .RequirePermission(LiensPermissions.LienSaleRead);
        seller.MapGet("/lookups/law-firms", GetLawFirms)
            .RequirePermission(LiensPermissions.LienSaleRead);
        seller.MapGet("/lookups/case-managers", GetCaseManagers)
            .RequirePermission(LiensPermissions.LienSaleRead);
        seller.MapGet("/lookups/facilities", GetFacilities)
            .RequirePermission(LiensPermissions.LienSaleRead);
        seller.MapGet("/lookups/medical-codes", GetMedicalCodes)
            .RequirePermission(LiensPermissions.LienSaleRead);
        seller.MapGet("/lookups/document-types", GetDocumentTypes)
            .RequirePermission(LiensPermissions.LienSaleRead);

        var buyer = app.MapGroup("/api/liens/selling/buyer")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        buyer.MapGet("/liens/by-lien/{lienId:guid}", GetBuyerLien)
            .AddEndpointFilter(new BuyerViewPermissionFilter());
        buyer.MapPost("/liens/by-lien/{lienId:guid}/offers", SubmitBuyerOffer)
            .RequirePermission(LiensPermissions.LienOffer);
        buyer.MapPost("/liens/by-lien/{lienId:guid}/decline", DeclineBuyerLien)
            .RequirePermission(LiensPermissions.LienOffer);
    }

    private static async Task<IResult> CreateLien(
        CreateSellingLienRequest request,
        HttpRequest httpRequest,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, userId) = RequireSellerContext(context);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError)) return idempotencyError!;
        var replay = await SellingIdempotency.GetReplayAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens", "SellerOrganization", sellerOrgId.ToString(), idempotencyKey!, request, ct);
        if (replay is not null) return replay;
        var sellerStatus = NormalizeIntakeStatus(request.SellerStatus);
        if (sellerStatus is null)
            return ValidationError("sellerStatus", "sellerStatus must be Pending or Internal.");

        var started = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens", "SellerOrganization", sellerOrgId.ToString(), idempotencyKey!, request, ct);
        if (started.Result is not null) return started.Result;

        var lien = Lien.Create(
            tenantId,
            sellerOrgId,
            $"SL-{Guid.CreateVersion7():N}"[..15].ToUpperInvariant(),
            LienType.MedicalLien,
            0m,
            userId,
            description: request.Source?.Trim());
        lien.UpdateSellingAnalyticsFields(userId, sellerStatus: sellerStatus);
        db.Liens.Add(lien);
        AddActivity(db, lien, userId, $"Selling lien created with status {sellerStatus}.");
        await db.SaveChangesAsync(ct);

        return await SellingIdempotency.CompleteAsync(db, started.Record!, userId, StatusCodes.Status201Created, new
        {
            lienId = lien.Id,
            lienNumber = lien.LienNumber,
            sellerStatus = lien.SellerStatus,
        }, ct);
    }

    private static async Task<IResult> GetLienDetail(
        Guid lienId,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, _) = RequireSellerContext(context);
        var lien = await GetSellerLienAsync(db, tenantId, sellerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);

        var caseEntity = lien.CaseId.HasValue
            ? await db.Cases.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == lien.CaseId.Value, ct)
            : null;
        var caseMetadata = ParseCaseMetadata(caseEntity?.Notes);
        var caseManagerId = ParseMetadataGuid(caseMetadata, "caseManagerId");
        var lawFirmId = ParseMetadataGuid(caseMetadata, "lawFirmId");
        var fundingCompany = lien.FundingCompanyId.HasValue
            ? await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == lien.FundingCompanyId.Value, ct)
            : null;
        var fundingContact = lien.FundingCompanyContactId.HasValue
            ? await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == lien.FundingCompanyContactId.Value, ct)
            : null;
        var caseManager = caseManagerId.HasValue
            ? await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == caseManagerId.Value, ct)
            : null;
        var lawFirm = lawFirmId.HasValue
            ? await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == lawFirmId.Value, ct)
            : caseEntity is null
                ? null
                : await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ContactType == ContactType.LawFirm && c.OrgId == caseEntity.OrgId, ct);
        var pricing = await db.ServicingItems.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.LienId == lien.Id && item.TaskType == SellingMedicalPricingTaskType)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => new { item.Id, item.Description, item.Notes, item.CreatedAtUtc })
            .ToListAsync(ct);
        var documents = await db.ServicingItems.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.LienId == lien.Id && item.TaskType == SellingDocumentTaskType)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => new { item.Id, item.Description, item.Notes })
            .ToListAsync(ct);
        var offers = await db.LienOffers.AsNoTracking()
            .Where(offer => offer.TenantId == tenantId && offer.LienId == lien.Id)
            .OrderByDescending(offer => offer.OfferAmount)
            .ToListAsync(ct);
        var activity = await db.LienStatusHistories.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.LienId == lien.Id)
            .OrderByDescending(item => item.ChangedAtUtc)
            .Take(100)
            .Select(item => new { item.Id, item.Description, item.ChangedByUserId, item.ChangedAtUtc })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            lienId = lien.Id,
            lienInformation = new
            {
                lien.LienNumber,
                lien.SellerStatus,
                lien.Status,
                lien.InitialServiceDate,
                lien.EndServiceDate,
                lien.ListingVisibility,
                lien.Notes,
                lien.BuyerMessage,
            },
            caseInformation = caseEntity is null ? null : new
            {
                caseEntity.Id,
                caseEntity.CaseNumber,
                caseEntity.Title,
                caseManagerId = caseManager?.Id,
                caseManagerName = caseManager?.DisplayName,
                lawFirmId = lawFirm?.Id,
                lawFirm = lawFirm is null ? null : DisplayName(lawFirm),
            },
            fundingCompany = fundingCompany is null && string.IsNullOrWhiteSpace(lien.ExternalReference) ? null : new
            {
                id = fundingCompany?.Id,
                name = fundingCompany is null ? lien.ExternalReference : DisplayName(fundingCompany),
                contactPerson = fundingContact?.DisplayName,
                emailAddress = fundingContact?.Email,
                contact = fundingContact is null ? null : new { fundingContact.Id, name = DisplayName(fundingContact) },
            },
            medicalPricing = new { lien.AskAmount, billingAmount = lien.OriginalAmount, rows = pricing },
            documents,
            saleReadiness = Readiness(lien, caseEntity is not null, pricing.Count, documents.Count),
            buyerOfferSummary = new
            {
                count = offers.Count,
                highestBidAmount = offers.Where(IsActiveOffer).Select(offer => offer.OfferAmount).DefaultIfEmpty().Max(),
            },
            activity,
            availableActions = AvailableActions(lien),
        });
    }

    private static async Task<IResult> GetLienActivity(
        Guid lienId,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, _) = RequireSellerContext(context);
        if (await GetSellerLienAsync(db, tenantId, sellerOrgId, lienId, ct) is null)
            return NotFoundLien(lienId);

        var items = await db.LienStatusHistories.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.LienId == lienId)
            .OrderByDescending(item => item.ChangedAtUtc)
            .Select(item => new
            {
                item.Id,
                eventType = "SellingLienActivity",
                item.Description,
                actorUserId = item.ChangedByUserId,
                timestampUtc = item.ChangedAtUtc,
            })
            .ToListAsync(ct);
        return Results.Ok(new { lienId, items });
    }

    private static async Task<IResult> SaveLienInformation(
        Guid lienId,
        SaveSellingLienInformationRequest request,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, userId) = RequireSellerContext(context);
        var lien = await GetSellerLienAsync(db, tenantId, sellerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        if (IntakeMutationBlocked(lien) is { } intakeError) return intakeError;

        var sellerStatus = NormalizeIntakeStatus(request.SellerStatus);
        if (sellerStatus is null) return ValidationError("sellerStatus", "sellerStatus must be Pending or Internal during intake.");
        var visibility = NormalizeVisibility(request.ListingVisibility);
        if (visibility is null) return ValidationError("listingVisibility", "listingVisibility must be Public or Private.");

        lien.Update(
            lien.LienType, lien.OriginalAmount, userId, lien.ExternalReference,
            lien.SubjectFirstName, lien.SubjectLastName, lien.IsConfidential, lien.Jurisdiction,
            lien.IncidentDate, request.InitialServiceDate, request.EndServiceDate,
            lien.IsBulk, lien.IsServicing, lien.Description, request.Notes);
        lien.UpdateSellingAnalyticsFields(userId, sellerStatus: sellerStatus, listingVisibility: visibility);
        AddActivity(db, lien, userId, "Selling lien information updated.");
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { lienId = lien.Id, lien.SellerStatus, lien.InitialServiceDate, lien.EndServiceDate, lien.ListingVisibility });
    }

    private static async Task<IResult> SaveCaseInformation(
        Guid lienId,
        SaveSellingCaseInformationRequest request,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, userId) = RequireSellerContext(context);
        var lien = await GetSellerLienAsync(db, tenantId, sellerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        if (IntakeMutationBlocked(lien) is { } intakeError) return intakeError;
        if (!request.FundingCompanyId.HasValue || request.FundingCompanyId.Value == Guid.Empty)
            return ValidationError("fundingCompanyId", "fundingCompanyId is required.");

        var fundingCompany = await GetFundingCompanyAsync(db, tenantId, request.FundingCompanyId.Value, ct);
        if (fundingCompany is null) return ValidationError("fundingCompanyId", "Funding company was not found in this tenant.");
        if (request.FundingCompanyContactId.HasValue)
        {
            var contact = await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c =>
                c.TenantId == tenantId && c.Id == request.FundingCompanyContactId.Value && c.IsActive, ct);
            if (contact is null || contact.OrgId != fundingCompany.OrgId)
                return ValidationError("fundingCompanyContactId", "Funding company contact must be active and belong to the selected funding company.");
        }

        Case? caseEntity = null;
        if (request.CaseId.HasValue && request.CaseId.Value != Guid.Empty)
        {
            caseEntity = await db.Cases.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == request.CaseId.Value, ct);
            if (caseEntity is null) return ValidationError("caseId", "Case was not found in this tenant.");
            if (caseEntity.OrgId != sellerOrgId)
                return ValidationError("caseId", "Case is not owned by the seller organization.");
        }
        else if (request.CreateCaseIfMissing)
        {
            caseEntity = Case.Create(tenantId, sellerOrgId, $"SC-{Guid.CreateVersion7():N}"[..15].ToUpperInvariant(), "Pending", "Lien", userId);
            db.Cases.Add(caseEntity);
        }
        else
        {
            return ValidationError("caseId", "caseId is required unless createCaseIfMissing is true.");
        }

        if (request.HandlingLawFirmId.HasValue && !await IsActiveContactAsync(db, tenantId, request.HandlingLawFirmId.Value, ContactType.LawFirm, ct))
            return ValidationError("handlingLawFirmId", "Handling law firm was not found in this tenant.");
        if (request.CaseManagerId.HasValue && !await IsActiveContactAsync(db, tenantId, request.CaseManagerId.Value, ContactType.CaseManager, ct))
            return ValidationError("caseManagerId", "Case manager was not found in this tenant.");

        lien.AttachCase(caseEntity.Id, userId);
        lien.UpdateSellingAnalyticsFields(userId, fundingCompanyId: fundingCompany.Id, fundingCompanyContactId: request.FundingCompanyContactId);
        if (request.CaseManagerId.HasValue) caseEntity.ReassignCaseManager(request.CaseManagerId.Value, userId);
        if (request.HandlingLawFirmId.HasValue) caseEntity.Update(
            caseEntity.ClientFirstName, caseEntity.ClientLastName, userId, caseEntity.Title, caseEntity.ExternalReference,
            caseEntity.ClientDob, caseEntity.ClientPhone, caseEntity.ClientEmail, caseEntity.ClientAddress,
            caseEntity.DateOfIncident, caseEntity.InsuranceCarrier, caseEntity.PolicyNumber, caseEntity.ClaimNumber,
            caseEntity.Description, AppendMetadata(caseEntity.Notes, "lawFirmId", request.HandlingLawFirmId.Value));
        AddActivity(db, lien, userId, "Selling case and funding-company information updated.");
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { lienId = lien.Id, caseId = lien.CaseId, fundingCompanyId = lien.FundingCompanyId, fundingCompanyContactId = lien.FundingCompanyContactId });
    }

    private static async Task<IResult> SaveMedicalPricing(
        Guid lienId,
        SaveSellingMedicalPricingRequest request,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, userId) = RequireSellerContext(context);
        var lien = await GetSellerLienAsync(db, tenantId, sellerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        if (IntakeMutationBlocked(lien) is { } intakeError) return intakeError;
        if (request.AskAmount is < 0 || request.BillingAmount is < 0 || request.Rows.Any(row =>
                row.BillingAmount < 0 || row.MedicareCost < 0 || row.TargetSaleAmount < 0))
            return ValidationError("medicalPricing", "Ask, billing, Medicare, and target sale amounts must be non-negative.");
        if (request.Rows.Any(row => string.IsNullOrWhiteSpace(row.MedicalCode)))
            return ValidationError("rows", "Every medical pricing row requires medicalCode.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var priorRows = await db.ServicingItems
            .Where(item => item.TenantId == tenantId && item.LienId == lien.Id && item.TaskType == SellingMedicalPricingTaskType)
            .ToListAsync(ct);
        db.ServicingItems.RemoveRange(priorRows);
        foreach (var row in request.Rows)
        {
            db.ServicingItems.Add(ServicingItem.Create(
                tenantId, sellerOrgId, $"SMP-{Guid.CreateVersion7():N}"[..15].ToUpperInvariant(),
                SellingMedicalPricingTaskType, row.MedicalCode.Trim(), "Selling", userId,
                caseId: lien.CaseId, lienId: lien.Id,
                notes: JsonSerializer.Serialize(new
                {
                    row.MedicalCode,
                    row.Description,
                    row.ServiceDate,
                    row.BillingAmount,
                    row.MedicareCost,
                    targetSaleAmount = row.TargetSaleAmount,
                })));
        }
        lien.SetFinancials(request.BillingAmount ?? lien.OriginalAmount, userId);
        lien.UpdateSellingAnalyticsFields(userId, askAmount: request.AskAmount);
        AddActivity(db, lien, userId, "Selling medical pricing updated.");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(new { lienId = lien.Id, lien.AskAmount, billingAmount = lien.OriginalAmount, rowCount = request.Rows.Count });
    }

    private static async Task<IResult> SaveDocuments(
        Guid lienId,
        SaveSellingDocumentsRequest request,
        LiensDbContext db,
        ISellingDocumentReferenceValidator documentValidator,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, userId) = RequireSellerContext(context);
        var lien = await GetSellerLienAsync(db, tenantId, sellerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        if (IntakeMutationBlocked(lien) is { } intakeError) return intakeError;
        if (request.Documents.Any(document => document.DocumentId == Guid.Empty || string.IsNullOrWhiteSpace(document.DocumentType)))
            return ValidationError("documents", "Each document requires a documentId and documentType.");

        foreach (var document in request.Documents)
        {
            if (!await documentValidator.IsAccessibleAsync(tenantId, sellerOrgId, userId, lien.Id, lien.CaseId, document.DocumentId, ct))
                return ValidationError("documents", $"Document '{document.DocumentId}' is unavailable or is not owned by this seller lien/case.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.ServicingItems.Where(item =>
            item.TenantId == tenantId && item.LienId == lien.Id && item.TaskType == SellingDocumentTaskType).ToListAsync(ct);
        db.ServicingItems.RemoveRange(existing);
        foreach (var document in request.Documents)
        {
            db.ServicingItems.Add(ServicingItem.Create(
                tenantId, sellerOrgId, $"SDR-{Guid.CreateVersion7():N}"[..15].ToUpperInvariant(),
                SellingDocumentTaskType, document.DisplayName?.Trim() ?? document.DocumentId.ToString(), "Selling", userId,
                caseId: lien.CaseId, lienId: lien.Id,
                notes: JsonSerializer.Serialize(new { document.DocumentId, document.DocumentType, document.DisplayName })));
        }
        AddActivity(db, lien, userId, "Selling document references updated.");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(new { lienId = lien.Id, documentCount = request.Documents.Count });
    }

    private static async Task<IResult> PrepareSale(
        Guid lienId,
        PrepareSellingLienRequest request,
        HttpRequest httpRequest,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, userId) = RequireSellerContext(context);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError)) return idempotencyError!;
        var replay = await SellingIdempotency.GetReplayAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens/{lienId}/prepare-sale", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (replay is not null) return replay;
        var lien = await GetSellerLienAsync(db, tenantId, sellerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        if (!IntakeStatuses.Contains(lien.SellerStatus ?? string.Empty))
            return ValidationError("sellerStatus", "Only Pending or Internal liens can be prepared for sale.");

        Contact? buyerContact = null;
        if (request.BuyerContactId is { } buyerContactId && buyerContactId != Guid.Empty)
        {
            buyerContact = await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c =>
                c.TenantId == tenantId && c.Id == buyerContactId && c.IsActive, ct);
            if (buyerContact is null)
                return ValidationError("buyerContactId", "Buyer contact must be active and belong to this tenant.");
        }
        var pricingRows = await db.ServicingItems.AnyAsync(item => item.TenantId == tenantId && item.LienId == lien.Id && item.TaskType == SellingMedicalPricingTaskType, ct);
        var documents = await db.ServicingItems.AnyAsync(item => item.TenantId == tenantId && item.LienId == lien.Id && item.TaskType == SellingDocumentTaskType, ct);
        if (!Readiness(lien, lien.CaseId.HasValue, pricingRows ? 1 : 0, documents ? 1 : 0, requireFundingCompany: false).ready)
            return ValidationError("saleReadiness", "Initial service date, case, pricing, ask amount, and at least one document are required before preparing a sale.");
        if (request.AskAmount is <= 0) return ValidationError("askAmount", "askAmount must be positive.");
        if (request.MessageToBuyer?.Trim().Length > 4000) return ValidationError("messageToBuyer", "messageToBuyer must not exceed 4000 characters.");
        var visibility = NormalizeVisibility(request.ListingVisibility);
        if (visibility is null) return ValidationError("listingVisibility", "listingVisibility must be Public or Private.");

        var started = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens/{lienId}/prepare-sale", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (started.Result is not null) return started.Result;

        lien.UpdateSellingAnalyticsFields(userId,
            sellerStatus: SellingLienStatus.PreparedForSale,
            listingVisibility: visibility,
            // A buyer is optional while preparing. When selected, its organization
            // is derived from the contact rather than a separate company record.
            fundingCompanyId: buyerContact?.OrgId,
            fundingCompanyContactId: buyerContact?.Id,
            askAmount: request.AskAmount);
        lien.SetBuyerMessage(request.MessageToBuyer, userId);
        AddActivity(db, lien, userId, "Lien prepared for sale.");
        await db.SaveChangesAsync(ct);
        return await SellingIdempotency.CompleteAsync(db, started.Record!, userId, StatusCodes.Status200OK,
            new { lienId = lien.Id, lien.SellerStatus, lien.AskAmount, lien.ListingVisibility }, ct);
    }

    private static async Task<IResult> ConfirmSale(
        Guid lienId,
        ConfirmSellingLienSaleRequest request,
        HttpRequest httpRequest,
        LiensDbContext db,
        ISellingPortfolioService service,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, userId) = RequireSellerContext(context);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError)) return idempotencyError!;
        var replay = await SellingIdempotency.GetReplayAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens/{lienId}/confirm-sale", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (replay is not null) return replay;
        var lien = await GetSellerLienAsync(db, tenantId, sellerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        if (!request.ConfirmationAccepted)
            return ValidationError("confirmationAccepted", "Confirm the sale before submitting it.");
        if (lien.SellerStatus != SellingLienStatus.PreparedForSale)
        {
            if (lien.SellerStatus == SellingLienStatus.SubmittedForSale)
                return Results.Conflict(new { error = new { code = "sale_already_submitted", message = "This lien has already been submitted for sale." } });
            return ValidationError("sellerStatus", "Only PreparedForSale liens can be confirmed for sale.");
        }

        // A second client key must not race the PreparedForSale -> Submitted
        // transition. This one-per-lien gate is persisted before invoking the
        // legacy service, whose notification/link work runs in its own unit of
        // work transaction.
        var transitionGate = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "LienTransition", lienId, "/api/liens/selling/liens/{lienId}/confirm-sale", "Lien", lienId.ToString(),
            "submit-for-sale-transition-v1", request: null, ct: ct);
        if (transitionGate.Result is not null)
        {
            return Results.Conflict(new { error = new { code = "sale_submission_in_progress", message = "This lien is already being submitted for sale. Retry with the original idempotency key." } });
        }

        var started = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens/{lienId}/confirm-sale", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (started.Result is not null) return started.Result;
        ConfirmSellingLienSaleResponse result;
        try
        {
            result = await service.ConfirmSaleAsync(tenantId, lienId, sellerOrgId, userId, request, ct);
        }
        catch
        {
            var transitioned = await db.Liens.AsNoTracking().AnyAsync(item =>
                item.TenantId == tenantId && item.Id == lienId &&
                item.Status == LienStatus.Offered && item.SellerStatus == SellingLienStatus.SubmittedForSale,
                ct);
            if (!transitioned)
            {
                db.SellingIdempotencyRecords.Remove(started.Record!);
                db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
                await db.SaveChangesAsync(ct);
            }
            throw;
        }
        // A newly generated portal URL contains a bearer capability. The
        // one-time API response may contain it, but durable replay data must
        // never retain that capability.
        var replayBody = new ConfirmSellingLienSaleResponse
        {
            LienId = result.LienId,
            LienCode = result.LienCode,
            Status = result.Status,
            SellerStatus = result.SellerStatus,
            AskAmount = result.AskAmount,
            OfferPrice = result.OfferPrice,
            SubmittedForSaleAtUtc = result.SubmittedForSaleAtUtc,
            SoldAtUtc = result.SoldAtUtc,
            Notification = result.Notification is null ? null : new ConfirmSellingLienBuyerNotificationResponse
            {
                Requested = result.Notification.Requested,
                Submitted = result.Notification.Submitted,
                NotificationId = result.Notification.NotificationId,
                NotificationStatus = result.Notification.NotificationStatus,
                FailureMessage = result.Notification.FailureMessage,
                BuyerAccessLinkId = result.Notification.BuyerAccessLinkId,
                BuyerPortalUrl = null,
                ExpiresAtUtc = result.Notification.ExpiresAtUtc,
                BuyerContactId = result.Notification.BuyerContactId,
                BuyerOrgId = result.Notification.BuyerOrgId,
                BuyerEmail = result.Notification.BuyerEmail,
            },
        };
        await SellingIdempotency.CompleteAsync(db, started.Record!, userId, StatusCodes.Status200OK, replayBody, ct);
        await SellingIdempotency.CompleteAsync(db, transitionGate.Record!, userId, StatusCodes.Status200OK, replayBody, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> WithdrawSale(
        Guid lienId,
        WithdrawSellingLienRequest request,
        HttpRequest httpRequest,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, userId) = RequireSellerContext(context);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError)) return idempotencyError!;
        var replay = await SellingIdempotency.GetReplayAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens/{lienId}/withdraw-sale", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (replay is not null) return replay;
        var lien = await GetSellerLienAsync(db, tenantId, sellerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        if (lien.SellerStatus != SellingLienStatus.SubmittedForSale || lien.Status != LienStatus.Offered)
            return ValidationError("sellerStatus", "Only SubmittedForSale liens can be withdrawn.");
        var lienTransition = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "LienStateTransition", lienId, "/api/liens/selling/liens/{lienId}/state-transition", "Lien", lienId.ToString(),
            "lien-state-transition-v1", request: null, ct: ct);
        if (lienTransition.Result is not null)
            return Results.Conflict(new { error = new { code = "lien_transition_in_progress", message = "This lien is changing state and cannot be withdrawn." } });
        var started = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens/{lienId}/withdraw-sale", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (started.Result is not null)
        {
            db.SellingIdempotencyRecords.Remove(lienTransition.Record!);
            await db.SaveChangesAsync(ct);
            return started.Result;
        }
        lien.Withdraw(userId);
        AddActivity(db, lien, userId, $"Sale withdrawn. {request.Reason}".Trim());
        await db.SaveChangesAsync(ct);
        var response = new { lienId = lien.Id, lien.SellerStatus, lien.Status, lien.WithdrawnAtUtc };
        var completed = await SellingIdempotency.CompleteAsync(db, started.Record!, userId, StatusCodes.Status200OK, response, ct);
        await SellingIdempotency.CompleteAsync(db, lienTransition.Record!, userId, StatusCodes.Status200OK, response, ct);
        return completed;
    }

    private static async Task<IResult> ArchiveLien(
        Guid lienId,
        ArchiveSellingLienRequest request,
        HttpRequest httpRequest,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, userId) = RequireSellerContext(context);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError)) return idempotencyError!;
        var replay = await SellingIdempotency.GetReplayAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens/{lienId}/archive", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (replay is not null) return replay;
        var lien = await GetSellerLienAsync(db, tenantId, sellerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        if (lien.SellerStatus is SellingLienStatus.Sold or SellingLienStatus.Archived)
            return ValidationError("sellerStatus", "Sold or already archived liens cannot be archived through this workflow.");
        var lienTransition = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "LienStateTransition", lienId, "/api/liens/selling/liens/{lienId}/state-transition", "Lien", lienId.ToString(),
            "lien-state-transition-v1", request: null, ct: ct);
        if (lienTransition.Result is not null)
            return Results.Conflict(new { error = new { code = "lien_transition_in_progress", message = "This lien is changing state and cannot be archived." } });
        var started = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens/{lienId}/archive", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (started.Result is not null)
        {
            db.SellingIdempotencyRecords.Remove(lienTransition.Record!);
            await db.SaveChangesAsync(ct);
            return started.Result;
        }
        lien.UpdateSellingAnalyticsFields(userId,
            sellerStatus: SellingLienStatus.Archived,
            archivedAtUtc: DateTime.UtcNow,
            archivedReason: request.Reason);
        AddActivity(db, lien, userId, "Lien archived.");
        await db.SaveChangesAsync(ct);
        var response = new { lienId = lien.Id, lien.SellerStatus, lien.ArchivedAtUtc, lien.ArchivedReason };
        var completed = await SellingIdempotency.CompleteAsync(db, started.Record!, userId, StatusCodes.Status200OK, response, ct);
        await SellingIdempotency.CompleteAsync(db, lienTransition.Record!, userId, StatusCodes.Status200OK, response, ct);
        return completed;
    }

    private static async Task<IResult> CreateBuyerAccessLink(
        Guid lienId,
        CreateSellingBuyerAccessLinkRequest request,
        HttpRequest httpRequest,
        LiensDbContext db,
        ISellingBuyerAccessLinkService links,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, sellerOrgId, userId) = RequireSellerContext(context);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError)) return idempotencyError!;
        var replay = await SellingIdempotency.GetReplayAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens/{lienId}/buyer-access-links", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (replay is not null) return replay;
        var lien = await GetSellerLienAsync(db, tenantId, sellerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        if (!IsSubmittedLien(lien)) return ValidationError("sellerStatus", "Buyer access links require a submitted-for-sale lien.");
        var buyerCompany = await GetFundingCompanyAsync(db, tenantId, request.BuyerFundingCompanyId, ct);
        var buyerContact = await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c =>
            c.TenantId == tenantId && c.Id == request.BuyerContactId && c.IsActive, ct);
        if (buyerCompany is null || buyerContact is null || buyerContact.OrgId != buyerCompany.OrgId)
            return ValidationError("buyerContactId", "The buyer funding company and contact must be active and related within this tenant.");
        var started = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "User", userId, "/api/liens/selling/liens/{lienId}/buyer-access-links", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (started.Result is not null) return started.Result;
        var expires = Math.Clamp(request.ExpiresInHours ?? 168, 1, 24 * 30);
        var result = await links.CreateAsync(tenantId, lien.Id, sellerOrgId, buyerCompany.OrgId, buyerContact.Id,
            userId, "/api/liens/selling/liens/{lienId}/buyer-access-links", idempotencyKey!, TimeSpan.FromHours(expires), ct);
        AddActivity(db, lien, userId, "Buyer access link created.");
        await db.SaveChangesAsync(ct);
        // The raw capability is intentionally returned exactly once. Persisted
        // retries replay a token-free completion snapshot, never the token.
        var safeReplay = new
        {
            accessLinkId = result.Id,
            token = (string?)null,
            buyerPortalUrl = (string?)null,
            result.ExpiresAtUtc,
            created = !result.AlreadyExisted,
        };
        await SellingIdempotency.CompleteAsync(db, started.Record!, userId, StatusCodes.Status200OK, safeReplay, ct);
        return Results.Ok(new
        {
            accessLinkId = result.Id,
            token = result.Token,
            buyerPortalUrl = result.Token is null ? null : result.BuyerPortalUrl,
            result.ExpiresAtUtc,
            created = !result.AlreadyExisted,
        });
    }

    private static async Task<IResult> GetBuyerLien(
        Guid lienId,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, buyerOrgId, _) = RequireBuyerContext(context);
        var lien = await ResolveGrantedBuyerLienAsync(db, tenantId, buyerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        return Results.Ok(new
        {
            lienId = lien.Id,
            lien.LienNumber,
            lien.Status,
            lien.SellerStatus,
            lien.InitialServiceDate,
            lien.EndServiceDate,
            lien.AskAmount,
            lien.OfferPrice,
            lien.OriginalAmount,
        });
    }

    private static async Task<IResult> SubmitBuyerOffer(
        Guid lienId,
        SubmitSellingBuyerOfferRequest request,
        HttpRequest httpRequest,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, buyerOrgId, userId) = RequireBuyerContext(context);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError)) return idempotencyError!;
        var replay = await SellingIdempotency.GetReplayAsync(
            db, tenantId, "User", userId, "/api/liens/selling/buyer/liens/by-lien/{lienId}/offers", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (replay is not null) return replay;
        var lien = await ResolveGrantedBuyerLienAsync(db, tenantId, buyerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        if (request.OfferAmount <= 0) return ValidationError("offerAmount", "offerAmount must be positive.");
        if (await db.LienOffers.AnyAsync(offer => offer.TenantId == tenantId && offer.LienId == lien.Id && offer.BuyerOrgId == buyerOrgId && offer.Status == OfferStatus.Pending && (!offer.ExpiresAtUtc.HasValue || offer.ExpiresAtUtc > DateTime.UtcNow), ct))
            return Results.Conflict(new { error = new { code = "active_offer_exists", message = "This buyer already has an active offer." } });
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        try
        {
            var started = await SellingIdempotency.TryBeginAsync(
                db, tenantId, "User", userId, "/api/liens/selling/buyer/liens/by-lien/{lienId}/offers", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
            if (started.Result is not null) return started.Result;
            var activeOfferExists = await db.LienOffers.AnyAsync(offer =>
                offer.TenantId == tenantId && offer.LienId == lien.Id && offer.BuyerOrgId == buyerOrgId &&
                offer.Status == OfferStatus.Pending && (!offer.ExpiresAtUtc.HasValue || offer.ExpiresAtUtc > DateTime.UtcNow), ct);
            if (activeOfferExists)
            {
                var conflict = await SellingIdempotency.CompleteAsync(db, started.Record!, userId, StatusCodes.Status409Conflict,
                    new { error = new { code = "active_offer_exists", message = "This buyer already has an active offer." } }, ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return conflict;
            }

            var offer = LienOffer.Create(tenantId, lien.Id, buyerOrgId, lien.SellingOrgId ?? lien.OrgId, request.OfferAmount, userId, request.Message);
            db.LienOffers.Add(offer);
            if (!lien.HighestBidAmount.HasValue || offer.OfferAmount > lien.HighestBidAmount.Value)
                lien.UpdateSellingAnalyticsFields(userId, highestBidAmount: offer.OfferAmount);
            await db.SaveChangesAsync(ct);
            var completed = await SellingIdempotency.CompleteAsync(db, started.Record!, userId, StatusCodes.Status201Created,
                new { offer.Id, offer.LienId, offer.OfferAmount, offer.Status, offer.OfferedAtUtc }, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return completed;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<IResult> DeclineBuyerLien(
        Guid lienId,
        DeclineSellingBuyerLienRequest request,
        HttpRequest httpRequest,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, buyerOrgId, userId) = RequireBuyerContext(context);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError)) return idempotencyError!;
        var replay = await SellingIdempotency.GetReplayAsync(
            db, tenantId, "User", userId, "/api/liens/selling/buyer/liens/by-lien/{lienId}/decline", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (replay is not null) return replay;
        var lien = await ResolveGrantedBuyerLienAsync(db, tenantId, buyerOrgId, lienId, ct);
        if (lien is null) return NotFoundLien(lienId);
        var link = await db.SellingBuyerAccessLinks.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.LienId == lienId && item.BuyerOrgId == buyerOrgId && !item.RevokedAtUtc.HasValue && item.ExpiresAtUtc > DateTime.UtcNow, ct);
        if (link is null) return NotFoundLien(lienId);

        if (!string.IsNullOrWhiteSpace(link.ResponseStatus))
        {
            return Results.Conflict(new
            {
                error = new
                {
                    code = "response_conflict",
                    message = "A buyer response has already been recorded for this access link.",
                },
            });
        }

        // This deliberately uses the public portal's response-transition identity.
        // An authenticated buyer and a token-link buyer can act on the same access
        // link, so both paths must contend on one per-link serialization gate.
        var responseTransition = await SellingIdempotency.TryBeginAsync(
            db,
            tenantId,
            "BuyerLinkResponseTransition",
            link.Id,
            "/api/liens/selling/public/{token}/response",
            "BuyerAccessLink",
            link.Id.ToString(),
            "buyer-response-transition-v1",
            request: null,
            ct: ct);
        if (responseTransition.Result is not null)
        {
            return Results.Conflict(new
            {
                error = new
                {
                    code = "response_conflict",
                    message = "A buyer response is already being recorded for this access link.",
                },
            });
        }

        var started = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "User", userId, "/api/liens/selling/buyer/liens/by-lien/{lienId}/decline", "Lien", lienId.ToString(), idempotencyKey!, request, ct);
        if (started.Result is not null)
        {
            db.SellingIdempotencyRecords.Remove(responseTransition.Record!);
            await db.SaveChangesAsync(ct);
            return started.Result;
        }

        // A buyer decline is recorded as a non-sale response; it does not mutate the core lien lifecycle.
        link.RecordResponse(SellingBuyerResponseStatus.Declined, null, request.Reason);
        AddActivity(db, lien, userId, "Buyer declined lien review.");
        await db.SaveChangesAsync(ct);
        var completed = await SellingIdempotency.CompleteAsync(db, started.Record!, userId, StatusCodes.Status200OK,
            new { lienId, response = SellingBuyerResponseStatus.Declined }, ct);
        await SellingIdempotency.CompleteAsync(db, responseTransition.Record!, userId, StatusCodes.Status200OK,
            new { lienId, response = SellingBuyerResponseStatus.Declined }, ct);
        return completed;
    }

    private static async Task<IResult> GetBulkImport(Guid importId, LiensDbContext db, ICurrentRequestContext context, CancellationToken ct)
    {
        var (tenantId, _, _) = RequireSellerContext(context);
        var batch = await GetSellingImportAsync(db, tenantId, importId, ct);
        return batch is null ? Results.NotFound() : Results.Ok(MapBulkImport(batch));
    }

    private static async Task<IResult> GetBulkImportRows(Guid importId, string? status, int page, int pageSize, LiensDbContext db, ICurrentRequestContext context, CancellationToken ct)
    {
        var (tenantId, _, _) = RequireSellerContext(context);
        if (page < 1 || pageSize is < 1 or > 100) return ValidationError("page", "page must be positive and pageSize must be between 1 and 100.");
        var batch = await GetSellingImportAsync(db, tenantId, importId, ct);
        if (batch is null) return Results.NotFound();
        var query = db.BatchUploadDetails.AsNoTracking().Where(row => row.TenantId == tenantId && row.BatchUploadId == batch.Id && row.RecordStatus == "A");
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            query = query.Where(row => row.Status == NormalizeRowStatus(status));
        var totalCount = await query.CountAsync(ct);
        var rows = await query.OrderBy(row => row.RowNumber).Skip((page - 1) * pageSize).Take(pageSize).Select(row => new { row.Id, row.RowNumber, row.Status, row.Reason, row.DataJson }).ToListAsync(ct);
        return Results.Ok(new { importId, page, pageSize, totalCount, items = rows });
    }

    private static async Task<IResult> ValidateBulkImport(Guid importId, LiensDbContext db, ICurrentRequestContext context, CancellationToken ct)
    {
        var (tenantId, _, userId) = RequireSellerContext(context);
        var batch = await GetSellingImportAsync(db, tenantId, importId, ct);
        if (batch is null) return Results.NotFound();
        if (batch.Status != "A") return Results.Conflict(new { error = new { code = "import_cancelled" } });
        if (batch.ProcessStatus is "CONFIRMED" or "PARTIAL")
            return Results.Conflict(new { error = new { code = "import_already_confirmed" } });
        var transitionGate = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "BulkImportTransition", importId, "/api/liens/selling/bulk-imports/{importId}/confirm-transition",
            "BulkImport", importId.ToString(), "bulk-import-confirm-transition-v1", request: null, ct: ct);
        if (transitionGate.Result is not null)
            return Results.Conflict(new { error = new { code = "import_transition_in_progress", message = "This bulk import is currently being confirmed or cancelled. Retry shortly." } });

        try
        {
            await db.Entry(batch).ReloadAsync(ct);
            if (batch.Status != "A")
            {
                db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
                await db.SaveChangesAsync(CancellationToken.None);
                return Results.Conflict(new { error = new { code = "import_cancelled" } });
            }
            if (batch.ProcessStatus is "CONFIRMED" or "PARTIAL")
            {
                db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
                await db.SaveChangesAsync(CancellationToken.None);
                return Results.Conflict(new { error = new { code = "import_already_confirmed" } });
            }

            var rows = await db.BatchUploadDetails.Where(row => row.TenantId == tenantId && row.BatchUploadId == batch.Id && row.RecordStatus == "A").ToListAsync(ct);
            foreach (var row in rows)
            {
                var values = JsonSerializer.Deserialize<Dictionary<string, string>>(row.DataJson) ?? [];
                var reason = ValidateImportRow(values);
                row.SetResult(reason is null ? "VALID" : "INVALID", reason, userId);
            }
            batch.SetProcessStatus(rows.Any(row => row.Status == "INVALID") ? "VALIDATED_WITH_ERRORS" : "VALIDATED", userId);
            await db.SaveChangesAsync(ct);
            var response = Results.Ok(new { importId, status = batch.ProcessStatus, validCount = rows.Count(row => row.Status == "VALID"), invalidCount = rows.Count(row => row.Status == "INVALID") });
            db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
            await db.SaveChangesAsync(CancellationToken.None);
            return response;
        }
        catch
        {
            db.ChangeTracker.Clear();
            db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<IResult> ConfirmBulkImport(Guid importId, HttpRequest httpRequest, LiensDbContext db, ICurrentRequestContext context, CancellationToken ct)
    {
        var (tenantId, sellerOrgId, userId) = RequireSellerContext(context);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError)) return idempotencyError!;
        var replay = await SellingIdempotency.GetReplayAsync(
            db, tenantId, "User", userId, "/api/liens/selling/bulk-imports/{importId}/confirm", "BulkImport", importId.ToString(), idempotencyKey!, request: null, ct);
        if (replay is not null) return replay;
        var batch = await GetSellingImportAsync(db, tenantId, importId, ct);
        if (batch is null) return Results.NotFound();
        if (batch.Status != "A") return Results.Conflict(new { error = new { code = "import_cancelled" } });
        if (batch.ProcessStatus is "CONFIRMED" or "PARTIAL")
            return Results.Conflict(new { error = new { code = "import_already_confirmed" } });
        var rows = await db.BatchUploadDetails.Where(row => row.TenantId == tenantId && row.BatchUploadId == batch.Id && row.RecordStatus == "A").ToListAsync(ct);
        if (rows.Any(row => row.Status == "PENDING")) return ValidationError("importId", "Validate the bulk import before confirming it.");
        if (rows.Any(row => row.Status == "INVALID")) return ValidationError("importId", "Correct invalid rows before confirming the bulk import.");
        var fundingCompanies = await db.Contacts.AsNoTracking()
            .Where(contact => contact.TenantId == tenantId && contact.IsActive &&
                (contact.ContactType == ContactType.FundingCompany || contact.ContactType == ContactType.LienHolder))
            .ToListAsync(ct);
        var medicalProviders = await db.Contacts.AsNoTracking()
            .Where(contact => contact.TenantId == tenantId && contact.IsActive && contact.ContactType == ContactType.Provider)
            .ToListAsync(ct);
        var facilities = await db.Facilities.AsNoTracking()
            .Where(facility => facility.TenantId == tenantId && facility.OrgId == sellerOrgId && facility.IsActive)
            .ToListAsync(ct);

        // A user idempotency key protects one caller. This batch-level gate also
        // prevents a second caller/key from creating the staged rows twice.
        var transitionGate = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "BulkImportTransition", importId, "/api/liens/selling/bulk-imports/{importId}/confirm-transition",
            "BulkImport", importId.ToString(), "bulk-import-confirm-transition-v1", request: null, ct: ct);
        if (transitionGate.Result is not null)
            return Results.Conflict(new { error = new { code = "import_confirmation_in_progress", message = "This bulk import is already being confirmed. Retry shortly." } });

        await db.Entry(batch).ReloadAsync(ct);
        if (batch.Status != "A")
        {
            db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
            await db.SaveChangesAsync(CancellationToken.None);
            return Results.Conflict(new { error = new { code = "import_cancelled" } });
        }
        if (batch.ProcessStatus is "CONFIRMED" or "PARTIAL")
        {
            db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
            await db.SaveChangesAsync(CancellationToken.None);
            return Results.Conflict(new { error = new { code = "import_already_confirmed" } });
        }

        var caseNumbers = rows.Where(row => row.Status == "VALID")
            .Select(row => GetImportValue(JsonSerializer.Deserialize<Dictionary<string, string>>(row.DataJson) ?? [], "Case Code*"))
            .Where(caseNumber => !string.IsNullOrWhiteSpace(caseNumber))
            .Select(caseNumber => caseNumber!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var casesByNumber = (await db.Cases.Where(caseEntity =>
                caseEntity.TenantId == tenantId && caseEntity.OrgId == sellerOrgId && caseNumbers.Contains(caseEntity.CaseNumber))
            .ToListAsync(ct))
            .GroupBy(caseEntity => caseEntity.CaseNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        SellingIdempotency.IdempotencyStart? started = null;
        try
        {
            started = await SellingIdempotency.TryBeginAsync(
                db, tenantId, "User", userId, "/api/liens/selling/bulk-imports/{importId}/confirm", "BulkImport", importId.ToString(), idempotencyKey!, request: null, ct);
            if (started.Result is not null)
            {
                db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
                await db.SaveChangesAsync(CancellationToken.None);
                return started.Result;
            }

            var created = 0;
            foreach (var row in rows.Where(row => row.Status == "VALID"))
            {
                Lien? lien = null;
                Case? createdCase = null;
                string? caseNumber = null;
                var rowEntities = new List<object>();
                try
                {
                    var values = JsonSerializer.Deserialize<Dictionary<string, string>>(row.DataJson) ?? [];
                    caseNumber = GetImportValue(values, "Case Code*")!;
                    if (!casesByNumber.TryGetValue(caseNumber, out var caseEntity))
                    {
                        createdCase = Case.Create(tenantId, sellerOrgId, caseNumber, "Pending", "Lien", userId,
                            externalReference: caseNumber, title: caseNumber);
                        db.Cases.Add(createdCase);
                        rowEntities.Add(createdCase);
                        caseEntity = createdCase;
                        casesByNumber[caseNumber] = caseEntity;
                    }
                    var fundingCompanyName = GetImportValue(values, "Funding Company");
                    var fundingCompany = ResolveImportContactByName(fundingCompanies, fundingCompanyName);
                    var facilityName = GetImportValue(values, "Facility Name*");
                    var facility = ResolveImportFacilityByName(facilities, facilityName);
                    var medicalProviderName = GetImportValue(values, "Medical Provider Name");
                    var medicalProvider = ResolveImportContactByName(medicalProviders, medicalProviderName);

                    var (medicalCode, medicalDescription) = ParseImportMedicalCode(GetImportValue(values, "Medical Code & Description*"));
                    lien = Lien.Create(tenantId, sellerOrgId, ResolveImportLienNumber(values), LienType.MedicalLien,
                        ParseImportDecimal(values, "Billing Amount*"), userId,
                        externalReference: fundingCompany?.Id.ToString() ?? fundingCompanyName,
                        facilityId: facility?.Id,
                        initialServiceDate: ParseImportDate(values, "Initial Service Date*"),
                        endServiceDate: ParseImportDate(values, "End Service Date"),
                        notes: GetImportValue(values, "Notes"));
                    lien.AttachCase(caseEntity.Id, userId);
                    lien.UpdateSellingAnalyticsFields(userId,
                        sellerStatus: NormalizeIntakeStatus(GetImportValue(values, "Seller Status")) ?? SellingLienStatus.Pending,
                        listingVisibility: NormalizeVisibility(GetImportValue(values, "Listing Visibility")) ?? SellingListingVisibility.Private,
                        fundingCompanyId: fundingCompany?.Id);
                    db.Liens.Add(lien);
                    rowEntities.Add(lien);
                    var pricing = ServicingItem.Create(
                        tenantId, sellerOrgId, $"SMP-{Guid.CreateVersion7():N}"[..15].ToUpperInvariant(),
                        SellingMedicalPricingTaskType, medicalCode, "Selling", userId,
                        caseId: caseEntity.Id, lienId: lien.Id,
                        notes: JsonSerializer.Serialize(new
                        {
                            medicalCode,
                            description = medicalDescription,
                            billingAmount = ParseImportDecimal(values, "Billing Amount*"),
                            medicareCost = ParseImportDecimal(values, "Medicare Cost"),
                            targetSaleAmount = ParseImportDecimal(values, "Purchase Amount*"),
                        }));
                    db.ServicingItems.Add(pricing);
                    rowEntities.Add(pricing);
                    var legacyMedicalCode = ServicingItem.Create(
                        tenantId, sellerOrgId, $"LMC-{Guid.CreateVersion7():N}"[..15].ToUpperInvariant(),
                        "LegacyMedicalCode", $"Medical code {medicalCode}", "system", userId,
                        caseId: caseEntity.Id, lienId: lien.Id,
                        notes: $"code={medicalCode}; description={medicalDescription}; medicareCost={GetImportValue(values, "Medicare Cost") ?? string.Empty}; billingAmount={GetImportValue(values, "Billing Amount*") ?? string.Empty}; purchaseAmount={GetImportValue(values, "Purchase Amount*") ?? string.Empty}; payee={GetImportValue(values, "Payee") ?? string.Empty}; outboundCheckNumber={GetImportValue(values, "Outbound Check Number") ?? string.Empty}");
                    db.ServicingItems.Add(legacyMedicalCode);
                    rowEntities.Add(legacyMedicalCode);
                    var facilityInfo = ServicingItem.Create(
                        tenantId, sellerOrgId, $"LMFI-{Guid.CreateVersion7():N}"[..15].ToUpperInvariant(),
                        "LegacyMedicalFacilityInfo", "Legacy medical facility information", "system", userId,
                        caseId: caseEntity.Id, lienId: lien.Id,
                        notes: $"facilityId={facility?.Id}; facilityName={facility?.Name ?? facilityName ?? string.Empty}; medicalProviderId={medicalProvider?.Id}; medicalProvider={medicalProvider?.Organization ?? medicalProvider?.DisplayName ?? medicalProviderName ?? string.Empty}");
                    db.ServicingItems.Add(facilityInfo);
                    rowEntities.Add(facilityInfo);
                    row.SetResult("CREATED", null, userId);
                    await db.SaveChangesAsync(ct);
                    created++;
                }
                catch (OperationCanceledException)
                {
                    DetachImportRowEntities(db, rowEntities);
                    if (createdCase is not null && caseNumber is not null) casesByNumber.Remove(caseNumber);
                    throw;
                }
                catch (Exception ex)
                {
                    DetachImportRowEntities(db, rowEntities);
                    if (createdCase is not null && caseNumber is not null) casesByNumber.Remove(caseNumber);
                    row.SetResult("FAILED", TruncateImportFailureReason(ex.Message), userId);
                }
            }
            batch.SetProcessStatus(rows.Any(row => row.Status == "FAILED") ? "PARTIAL" : "CONFIRMED", userId);
            await db.SaveChangesAsync(ct);
            var response = new { importId, status = batch.ProcessStatus, createdCount = created, failedCount = rows.Count(row => row.Status == "FAILED") };
            var completed = await SellingIdempotency.CompleteAsync(db, started.Record!, userId, StatusCodes.Status200OK, response, ct);
            await SellingIdempotency.CompleteAsync(db, transitionGate.Record!, userId, StatusCodes.Status200OK, response, ct);
            return completed;
        }
        catch
        {
            // A completed row is saved with its matching lien, so releasing this
            // gate permits a safe retry after cancellation or an infrastructure
            // failure without duplicating the rows that already succeeded.
            db.ChangeTracker.Clear();
            if (started?.Record is not null && started.Record.ProcessingState != SellingIdempotencyRecord.Completed)
                db.SellingIdempotencyRecords.Remove(started.Record);
            db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<IResult> CancelBulkImport(Guid importId, LiensDbContext db, ICurrentRequestContext context, CancellationToken ct)
    {
        var (tenantId, _, userId) = RequireSellerContext(context);
        var batch = await GetSellingImportAsync(db, tenantId, importId, ct);
        if (batch is null) return Results.NotFound();
        if (batch.ProcessStatus is "CONFIRMED" or "PARTIAL") return Results.Conflict(new { error = new { code = "import_already_confirmed" } });
        var transitionGate = await SellingIdempotency.TryBeginAsync(
            db, tenantId, "BulkImportTransition", importId, "/api/liens/selling/bulk-imports/{importId}/confirm-transition",
            "BulkImport", importId.ToString(), "bulk-import-confirm-transition-v1", request: null, ct: ct);
        if (transitionGate.Result is not null)
            return Results.Conflict(new { error = new { code = "import_confirmation_in_progress", message = "This bulk import is being confirmed and cannot be cancelled." } });

        try
        {
            await db.Entry(batch).ReloadAsync(ct);
            if (batch.ProcessStatus is "CONFIRMED" or "PARTIAL")
            {
                db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
                await db.SaveChangesAsync(CancellationToken.None);
                return Results.Conflict(new { error = new { code = "import_already_confirmed" } });
            }
            if (batch.Status != "A")
            {
                db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
                await db.SaveChangesAsync(CancellationToken.None);
                return Results.Conflict(new { error = new { code = "import_cancelled" } });
            }

            batch.Deactivate(userId);
            batch.SetProcessStatus("CANCELLED", userId);
            await db.SaveChangesAsync(ct);
            await SellingIdempotency.CompleteAsync(db, transitionGate.Record!, userId, StatusCodes.Status200OK,
                new { importId, status = "CANCELLED" }, ct);
            return Results.NoContent();
        }
        catch
        {
            db.ChangeTracker.Clear();
            db.SellingIdempotencyRecords.Remove(transitionGate.Record!);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<IResult> GetFundingCompanies(LiensDbContext db, ICurrentRequestContext context, CancellationToken ct)
    {
        var (tenantId, _, _) = RequireSellerContext(context);
        var items = await db.Contacts.AsNoTracking().Where(c => c.TenantId == tenantId && c.IsActive && (c.ContactType == ContactType.FundingCompany || c.ContactType == ContactType.LienHolder)).OrderBy(c => c.Organization).Select(c => new { c.Id, name = DisplayName(c), c.OrgId }).ToListAsync(ct);
        return Results.Ok(new { items });
    }

    private static async Task<IResult> GetFundingCompanyContacts(Guid fundingCompanyId, LiensDbContext db, ICurrentRequestContext context, CancellationToken ct)
    {
        var (tenantId, _, _) = RequireSellerContext(context);
        var company = await GetFundingCompanyAsync(db, tenantId, fundingCompanyId, ct);
        if (company is null) return ValidationError("fundingCompanyId", "Funding company was not found in this tenant.");
        var items = await db.Contacts.AsNoTracking().Where(c => c.TenantId == tenantId && c.OrgId == company.OrgId && c.IsActive).OrderBy(c => c.DisplayName).Select(c => new { c.Id, name = DisplayName(c), c.Email }).ToListAsync(ct);
        return Results.Ok(new { items });
    }

    private static async Task<IResult> GetLawFirms(LiensDbContext db, ICurrentRequestContext context, CancellationToken ct) => await GetContactsByType(db, context, ContactType.LawFirm, null, ct);
    private static async Task<IResult> GetCaseManagers(Guid? lawFirmId, LiensDbContext db, ICurrentRequestContext context, CancellationToken ct) => await GetContactsByType(db, context, ContactType.CaseManager, lawFirmId, ct);
    private static async Task<IResult> GetContactsByType(LiensDbContext db, ICurrentRequestContext context, string type, Guid? lawFirmId, CancellationToken ct)
    {
        var (tenantId, _, _) = RequireSellerContext(context);
        var query = db.Contacts.AsNoTracking().Where(c => c.TenantId == tenantId && c.ContactType == type && c.IsActive);
        if (lawFirmId.HasValue) query = query.Where(c => c.LawFirmId == lawFirmId || c.OrgId == lawFirmId);
        var items = await query.OrderBy(c => c.DisplayName).Select(c => new { c.Id, name = DisplayName(c), c.OrgId }).ToListAsync(ct);
        return Results.Ok(new { items });
    }
    private static async Task<IResult> GetFacilities(LiensDbContext db, ICurrentRequestContext context, CancellationToken ct)
    {
        var (tenantId, sellerOrgId, _) = RequireSellerContext(context);
        var items = await db.Facilities.AsNoTracking().Where(f => f.TenantId == tenantId && f.IsActive && f.OrgId == sellerOrgId).OrderBy(f => f.Name).Select(f => new { f.Id, f.Name, f.Code }).ToListAsync(ct);
        return Results.Ok(new { items });
    }
    private static async Task<IResult> GetMedicalCodes(string? search, LiensDbContext db, ICurrentRequestContext context, CancellationToken ct)
    {
        var (tenantId, _, _) = RequireSellerContext(context);
        var query = db.ManualMedicalCodes.AsNoTracking().Where(code => code.TenantId == tenantId && code.Status == "A");
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(code => code.Code.Contains(search.Trim()) || (code.Description != null && code.Description.Contains(search.Trim())));
        var items = await query.OrderBy(code => code.Code).Take(100).Select(code => new { code.Id, code.Code, code.Description, code.Cost }).ToListAsync(ct);
        return Results.Ok(new { items });
    }
    private static IResult GetDocumentTypes() => Results.Ok(new { items = new[] { "MedicalBill", "MedicalRecord", "LienAgreement", "SettlementStatement", "Other" } });

    private static async Task<Lien?> GetSellerLienAsync(LiensDbContext db, Guid tenantId, Guid sellerOrgId, Guid lienId, CancellationToken ct) =>
        await db.Liens.FirstOrDefaultAsync(lien => lien.TenantId == tenantId && lien.Id == lienId &&
            (lien.SellingOrgId == sellerOrgId || (lien.SellingOrgId == null && lien.OrgId == sellerOrgId)), ct);

    private static async Task<Lien?> ResolveGrantedBuyerLienAsync(LiensDbContext db, Guid tenantId, Guid buyerOrgId, Guid lienId, CancellationToken ct)
    {
        var lien = await db.Liens.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == lienId, ct);
        if (lien is null || !IsSubmittedLien(lien)) return null;
        var granted = await db.SellingBuyerAccessLinks.AsNoTracking().AnyAsync(link => link.TenantId == tenantId && link.LienId == lienId && link.BuyerOrgId == buyerOrgId && !link.RevokedAtUtc.HasValue && link.ExpiresAtUtc > DateTime.UtcNow, ct);
        return granted ? lien : null;
    }

    private static bool IsSubmittedLien(Lien lien) => lien.Status == LienStatus.Offered && lien.SellerStatus == SellingLienStatus.SubmittedForSale && lien.ArchivedAtUtc is null && lien.SoldAtUtc is null && lien.WithdrawnAtUtc is null;
    private static bool IsActiveOffer(LienOffer offer) => offer.Status is not OfferStatus.Rejected and not OfferStatus.Withdrawn and not OfferStatus.Expired && !offer.IsExpired;
    private static async Task<Contact?> GetFundingCompanyAsync(LiensDbContext db, Guid tenantId, Guid id, CancellationToken ct) => await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id && c.IsActive && (c.ContactType == ContactType.FundingCompany || c.ContactType == ContactType.LienHolder), ct);
    private static async Task<bool> IsActiveContactAsync(LiensDbContext db, Guid tenantId, Guid id, string type, CancellationToken ct) => await db.Contacts.AsNoTracking().AnyAsync(c => c.TenantId == tenantId && c.Id == id && c.IsActive && c.ContactType == type, ct);
    private static (Guid TenantId, Guid OrgId, Guid UserId) RequireSellerContext(ICurrentRequestContext context) => (context.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required."), context.OrgId ?? throw new UnauthorizedAccessException("Organization context is required."), context.UserId ?? throw new UnauthorizedAccessException("User context is required."));
    private static (Guid TenantId, Guid OrgId, Guid UserId) RequireBuyerContext(ICurrentRequestContext context) => RequireSellerContext(context);
    private static string? NormalizeIntakeStatus(string? status) => IntakeStatuses.FirstOrDefault(candidate => string.Equals(candidate, status?.Trim(), StringComparison.OrdinalIgnoreCase));
    private static IResult? IntakeMutationBlocked(Lien lien) => IntakeStatuses.Contains(lien.SellerStatus ?? string.Empty)
        ? null
        : Results.Conflict(new { error = new { code = "intake_locked", message = "Lien intake can be changed only while sellerStatus is Pending or Internal." } });
    private static string? NormalizeVisibility(string? visibility) => SellingListingVisibility.All.FirstOrDefault(candidate => string.Equals(candidate, visibility?.Trim(), StringComparison.OrdinalIgnoreCase));
    private static IResult ValidationError(string key, string message) => Results.BadRequest(new { error = new { code = "validation_error", message, errors = new Dictionary<string, string[]> { [key] = [message] } } });
    private static IResult NotFoundLien(Guid lienId) => Results.NotFound(new { error = new { code = "not_found", message = $"Lien '{lienId}' was not found." } });
    private static bool HasIdempotencyKey(HttpRequest request, out IResult? error, out string? key)
    {
        key = request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        error = string.IsNullOrWhiteSpace(key) ? ValidationError("Idempotency-Key", "Idempotency-Key header is required.") : null;
        return error is null;
    }
    private static bool HasIdempotencyKey(HttpRequest request, out IResult? error) => HasIdempotencyKey(request, out error, out _);
    private static void AddActivity(LiensDbContext db, Lien lien, Guid userId, string description) => db.LienStatusHistories.Add(LienStatusHistory.Create(lien.TenantId, lien.Id, lien.CaseId, description, userId));
    private static string DisplayName(Contact contact) => string.IsNullOrWhiteSpace(contact.Organization) ? contact.DisplayName : contact.Organization;
    private static (bool ready, string[] missing) Readiness(
        Lien lien,
        bool hasCase,
        int pricingRows,
        int documents,
        bool requireFundingCompany = true)
    {
        var missing = new List<string>();
        if (!lien.InitialServiceDate.HasValue) missing.Add("initialServiceDate");
        if (!hasCase) missing.Add("caseInformation");
        if (requireFundingCompany && !lien.FundingCompanyId.HasValue) missing.Add("fundingCompany");
        if (!lien.AskAmount.HasValue || lien.AskAmount.Value <= 0m) missing.Add("askAmount");
        if (pricingRows == 0) missing.Add("medicalPricing");
        if (documents == 0) missing.Add("documents");
        return (missing.Count == 0, missing.ToArray());
    }
    private static string[] AvailableActions(Lien lien) => lien.SellerStatus switch
    {
        SellingLienStatus.Pending or SellingLienStatus.Internal => ["prepare-sale", "archive"],
        SellingLienStatus.PreparedForSale => ["confirm-sale", "archive"],
        SellingLienStatus.SubmittedForSale => ["withdraw-sale", "archive", "buyer-access-links"],
        _ => [],
    };
    private static Dictionary<string, string> ParseCaseMetadata(string? notes)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(notes)) return metadata;

        const string legacyMetadataMarker = "[legacy-meta]";
        var markerIndex = notes.IndexOf(legacyMetadataMarker, StringComparison.Ordinal);
        var rawMetadata = markerIndex >= 0 ? notes[(markerIndex + legacyMetadataMarker.Length)..].Trim() : notes;
        foreach (var segment in rawMetadata.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex > 0)
                metadata[segment[..separatorIndex].Trim()] = segment[(separatorIndex + 1)..].Trim();
        }

        return metadata;
    }
    private static Guid? ParseMetadataGuid(IReadOnlyDictionary<string, string> metadata, string key)
        => metadata.TryGetValue(key, out var value) && Guid.TryParse(value, out var id) ? id : null;
    private static string AppendMetadata(string? notes, string key, Guid value)
    {
        var map = (notes ?? string.Empty).Split("; ", StringSplitOptions.RemoveEmptyEntries).Where(segment => segment.Contains('='))
            .Select(segment => segment.Split('=', 2)).ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
        map[key] = value.ToString();
        return string.Join("; ", map.Select(pair => $"{pair.Key}={pair.Value}"));
    }
    private static async Task<BatchUpload?> GetSellingImportAsync(LiensDbContext db, Guid tenantId, Guid id, CancellationToken ct) => await db.BatchUploads.FirstOrDefaultAsync(batch => batch.TenantId == tenantId && batch.Id == id && batch.Template == "SellingLienImport", ct);
    private static object MapBulkImport(BatchUpload batch) => new { importId = batch.Id, status = batch.ProcessStatus, batch.Rows, batch.FileName, batch.CreatedAtUtc, batch.CreatedByUserId, batch.UpdatedAtUtc };
    private static string NormalizeRowStatus(string status) => status.Trim().ToLowerInvariant() switch { "valid" => "VALID", "invalid" => "INVALID", "created" => "CREATED", "failed" => "FAILED", _ => status.Trim().ToUpperInvariant() };
    private static string TruncateImportFailureReason(string message) => message.Length <= 4000 ? message : message[..4000];
    private static void DetachImportRowEntities(LiensDbContext db, IEnumerable<object> entities)
    {
        foreach (var entity in entities)
            db.Entry(entity).State = EntityState.Detached;
    }
    private static string? ValidateImportRow(IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(GetImportValue(values, "Case Code*"))) return "Case Code* is required.";
        if (ParseImportDate(values, "Initial Service Date*") is null) return "Initial Service Date* must be a valid date.";
        if (string.IsNullOrWhiteSpace(GetImportValue(values, "Facility Name*"))) return "Facility Name* is required.";
        if (string.IsNullOrWhiteSpace(GetImportValue(values, "Medical Code & Description*"))) return "Medical Code & Description* is required.";
        if (!TryParseImportDecimal(values, "Billing Amount*", out var billing) || billing < 0m) return "Billing Amount* must be a non-negative decimal.";
        return null;
    }
    private static string ResolveImportLienNumber(IReadOnlyDictionary<string, string> values) => $"SL-{Guid.CreateVersion7():N}"[..15].ToUpperInvariant();
    private static Contact? ResolveImportContactByName(IEnumerable<Contact> contacts, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var matches = contacts.Where(contact => ImportContactNameMatches(contact, name)).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }
    private static Facility? ResolveImportFacilityByName(IEnumerable<Facility> facilities, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var matches = facilities.Where(facility => ImportFacilityNameMatches(facility, name)).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }
    private static bool ImportContactNameMatches(Contact contact, string name)
        => string.Equals(contact.Organization, name.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(contact.DisplayName, name.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool ImportFacilityNameMatches(Facility facility, string name)
        => string.Equals(facility.Name, name.Trim(), StringComparison.OrdinalIgnoreCase);
    private static (string Code, string Description) ParseImportMedicalCode(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        var separator = normalized.IndexOf(" - ", StringComparison.Ordinal);
        return separator > 0
            ? (normalized[..separator].Trim(), normalized[(separator + 3)..].Trim())
            : (normalized, string.Empty);
    }
    private static string? GetImportValue(IReadOnlyDictionary<string, string> values, string key) => values.FirstOrDefault(pair => string.Equals(pair.Key.Trim(), key, StringComparison.OrdinalIgnoreCase)).Value?.Trim();
    private static bool TryParseImportDecimal(IReadOnlyDictionary<string, string> values, string key, out decimal value) => decimal.TryParse(GetImportValue(values, key), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    private static decimal ParseImportDecimal(IReadOnlyDictionary<string, string> values, string key) => TryParseImportDecimal(values, key, out var value) ? value : 0m;
    private static DateOnly? ParseImportDate(IReadOnlyDictionary<string, string> values, string key) => DateOnly.TryParse(GetImportValue(values, key), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private sealed class BuyerViewPermissionFilter : IEndpointFilter
    {
        public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var user = context.HttpContext.User;
            if (!user.HasPermission(LiensPermissions.LienBrowse) && !user.HasPermission(LiensPermissions.LienReadHeld))
                return ValueTask.FromResult<object?>(Results.Forbid());
            return next(context);
        }
    }
}
