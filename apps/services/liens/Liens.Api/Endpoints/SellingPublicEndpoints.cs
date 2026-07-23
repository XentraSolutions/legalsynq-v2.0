using System.Globalization;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Api.Endpoints;

public static class SellingPublicEndpoints
{
    private static readonly string[] DocumentTaskTypes =
    [
        "LegacyCaseDocument",
        "LegacyLienDocument",
        "LegacyMedicalDocument",
    ];

    public static void MapSellingPublicEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/selling/public")
            .AllowAnonymous();

        group.MapGet("/{token}", GetTemporaryBuyerPortal)
            .AllowAnonymous();

        group.MapPost("/{token}/accept", AcceptTemporaryBuyerPortal)
            .AllowAnonymous();

        group.MapPost("/{token}/offers", AcceptTemporaryBuyerPortal)
            .AllowAnonymous();

        group.MapPost("/{token}/decline", DeclineTemporaryBuyerPortal)
            .AllowAnonymous();
    }

    private static async Task<IResult> GetTemporaryBuyerPortal(
        string token,
        LiensDbContext db,
        CancellationToken ct = default)
    {
        var resolved = await ResolvePublicAccessLinkAsync(token, db, ct);
        if (resolved.Error is not null)
            return resolved.Error;

        var view = await BuildPublicViewAsync(db, resolved.AccessLink!, ct);
        if (view is null)
        {
            return PublicLinkState(
                "unavailable",
                "Lien offer unavailable",
                "The lien offer data could not be resolved.",
                StatusCodes.Status404NotFound);
        }

        resolved.AccessLink!.MarkAccessed();
        await db.SaveChangesAsync(ct);

        return Results.Ok(MapPublicPortalResponse(view));
    }

    private static async Task<IResult> AcceptTemporaryBuyerPortal(
        string token,
        PublicBuyerAcceptLienRequest? request,
        HttpContext httpContext,
        LiensDbContext db,
        CancellationToken ct = default)
    {
        var resolved = await ResolvePublicAccessLinkAsync(token, db, ct);
        if (resolved.Error is not null)
            return resolved.Error;

        var view = await BuildPublicViewAsync(db, resolved.AccessLink!, ct);
        if (view is null)
        {
            return PublicLinkState(
                "unavailable",
                "Lien offer unavailable",
                "The lien offer data could not be resolved.",
                StatusCodes.Status404NotFound);
        }

        var responseAmount = view.Lien.AskAmount;
        if (!responseAmount.HasValue || responseAmount.Value <= 0m)
        {
            return PublicLinkState(
                "ask-unavailable",
                "Lien offer unavailable",
                "This lien does not have a valid ask amount.",
                StatusCodes.Status409Conflict);
        }

        return await RecordPublicResponseAsync(
            db,
            view,
            SellingBuyerResponseStatus.Accepted,
            responseAmount.Value,
            FirstNonEmpty(request?.Notes, request?.Message),
            ReadIdempotencyKey(httpContext),
            ct);
    }

    private static async Task<IResult> DeclineTemporaryBuyerPortal(
        string token,
        PublicBuyerDeclineLienRequest? request,
        HttpContext httpContext,
        LiensDbContext db,
        CancellationToken ct = default)
    {
        var resolved = await ResolvePublicAccessLinkAsync(token, db, ct);
        if (resolved.Error is not null)
            return resolved.Error;

        var view = await BuildPublicViewAsync(db, resolved.AccessLink!, ct);
        if (view is null)
        {
            return PublicLinkState(
                "unavailable",
                "Lien offer unavailable",
                "The lien offer data could not be resolved.",
                StatusCodes.Status404NotFound);
        }

        return await RecordPublicResponseAsync(
            db,
            view,
            SellingBuyerResponseStatus.Declined,
            responseAmount: null,
            responseNotes: request?.Reason,
            responseIdempotencyKey: ReadIdempotencyKey(httpContext),
            ct);
    }

    private static async Task<(SellingBuyerAccessLink? AccessLink, IResult? Error)> ResolvePublicAccessLinkAsync(
        string token,
        LiensDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, PublicLinkState(
                "missing-token",
                "Lien offer link unavailable",
                "The secure link is missing from this request.",
                StatusCodes.Status404NotFound));
        }

        var accessLink = await db.SellingBuyerAccessLinks
            .FirstOrDefaultAsync(link => link.Token == token.Trim(), ct);

        if (accessLink is null)
        {
            return (null, PublicLinkState(
                "not-found",
                "Lien offer link unavailable",
                "The secure link could not be found.",
                StatusCodes.Status404NotFound));
        }

        if (accessLink.RevokedAtUtc.HasValue)
        {
            return (null, PublicLinkState(
                "revoked",
                "Lien offer link revoked",
                "This secure link is no longer active.",
                StatusCodes.Status410Gone));
        }

        if (accessLink.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return (null, PublicLinkState(
                "expired",
                "Lien offer link expired",
                "This secure link has expired.",
                StatusCodes.Status410Gone));
        }

        return (accessLink, null);
    }

    private static async Task<IResult> RecordPublicResponseAsync(
        LiensDbContext db,
        PublicPortalView view,
        string responseStatus,
        decimal? responseAmount,
        string? responseNotes,
        string? responseIdempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(view.AccessLink.ResponseStatus))
        {
            if (string.Equals(view.AccessLink.ResponseStatus, responseStatus, StringComparison.Ordinal))
            {
                await ApplyPublicResponseToLienAsync(db, view, responseStatus, ct);
                await db.SaveChangesAsync(ct);

                var reconciledView = await BuildPublicViewAsync(db, view.AccessLink, ct) ?? view;
                return Results.Ok(MapPublicPortalResponse(reconciledView));
            }

            return PublicLinkState(
                "response-conflict",
                "Lien response already recorded",
                "A different response has already been securely recorded for this lien offer.",
                StatusCodes.Status409Conflict);
        }

        if (!IsActionableLienStatus(view.Lien.Status))
        {
            return PublicLinkState(
                "not-actionable",
                "Lien offer unavailable",
                "This lien is no longer accepting buyer responses.",
                StatusCodes.Status409Conflict);
        }

        view.AccessLink.MarkAccessed();
        view.AccessLink.RecordResponse(
            responseStatus,
            responseAmount,
            responseNotes,
            responseIdempotencyKey);

        await ApplyPublicResponseToLienAsync(db, view, responseStatus, ct);
        await db.SaveChangesAsync(ct);

        var updatedView = await BuildPublicViewAsync(db, view.AccessLink, ct) ?? view;
        return Results.Ok(MapPublicPortalResponse(updatedView));
    }

    private static async Task ApplyPublicResponseToLienAsync(
        LiensDbContext db,
        PublicPortalView view,
        string responseStatus,
        CancellationToken ct)
    {
        var lien = await db.Liens
            .FirstOrDefaultAsync(l =>
                l.TenantId == view.AccessLink.TenantId &&
                l.Id == view.AccessLink.LienId,
                ct);

        if (lien is null)
            return;

        var updatedByUserId = ResolvePublicResponseActorId(view.AccessLink);
        if (string.Equals(responseStatus, SellingBuyerResponseStatus.Accepted, StringComparison.Ordinal))
        {
            if (IsActionableLienStatus(lien.Status))
                lien.TransitionStatus(LienStatus.Accepted, updatedByUserId);

            if (string.Equals(lien.Status, LienStatus.Accepted, StringComparison.Ordinal) &&
                !string.Equals(lien.SellerStatus, SellingLienStatus.Accepted, StringComparison.Ordinal))
                lien.UpdateSellingAnalyticsFields(updatedByUserId, sellerStatus: SellingLienStatus.Accepted);
        }
        else if (string.Equals(responseStatus, SellingBuyerResponseStatus.Declined, StringComparison.Ordinal))
        {
            if (IsActionableLienStatus(lien.Status))
                lien.TransitionStatus(LienStatus.Declined, updatedByUserId);
            else if (string.Equals(lien.Status, LienStatus.Withdrawn, StringComparison.Ordinal))
                lien.SetLegacyMedicalStatus(LienStatus.Declined, updatedByUserId);

            if (string.Equals(lien.Status, LienStatus.Declined, StringComparison.Ordinal) &&
                !string.Equals(lien.SellerStatus, SellingLienStatus.Declined, StringComparison.Ordinal))
                lien.UpdateSellingAnalyticsFields(updatedByUserId, sellerStatus: SellingLienStatus.Declined);
        }
    }

    private static Guid ResolvePublicResponseActorId(SellingBuyerAccessLink accessLink)
        => accessLink.CreatedByUserId.GetValueOrDefault(accessLink.BuyerContactId);

    private static async Task<PublicPortalView?> BuildPublicViewAsync(
        LiensDbContext db,
        SellingBuyerAccessLink accessLink,
        CancellationToken ct)
    {
        var lien = await db.Liens
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == accessLink.TenantId && l.Id == accessLink.LienId, ct);

        if (lien is null)
            return null;

        var caseEntity = lien.CaseId.HasValue
            ? await db.Cases
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == accessLink.TenantId && c.Id == lien.CaseId.Value, ct)
            : null;

        var buyerContact = await db.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.TenantId == accessLink.TenantId &&
                c.Id == accessLink.BuyerContactId &&
                c.OrgId == accessLink.BuyerOrgId,
                ct);

        var sellerContacts = await db.Contacts
            .AsNoTracking()
            .Where(c => c.TenantId == accessLink.TenantId && c.OrgId == accessLink.SellerOrgId && c.IsActive)
            .ToListAsync(ct);

        var sellerContact = SelectSellerContact(sellerContacts);
        var handlingLawFirm = await ResolveHandlingLawFirmAsync(db, accessLink.TenantId, caseEntity, ct);
        var caseManager = await ResolveCaseManagerAsync(db, accessLink.TenantId, caseEntity, ct);
        var documents = await ResolveDocumentsAsync(db, accessLink.TenantId, lien, caseEntity, ct);

        return new PublicPortalView(
            accessLink,
            lien,
            caseEntity,
            buyerContact,
            sellerContact,
            handlingLawFirm,
            caseManager,
            documents);
    }

    private static async Task<string?> ResolveHandlingLawFirmAsync(
        LiensDbContext db,
        Guid tenantId,
        Case? caseEntity,
        CancellationToken ct)
    {
        if (caseEntity is null)
            return null;

        var metadata = ParseLegacyNoteFields(caseEntity.Notes);
        if (Guid.TryParse(metadata.GetValueOrDefault("lawFirmId"), out var lawFirmId))
        {
            var lawFirm = await db.Contacts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == lawFirmId, ct);
            var name = FirstNonEmpty(lawFirm?.Organization, lawFirm?.DisplayName);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        var defaultLawFirm = await db.Contacts
            .AsNoTracking()
            .Where(c =>
                c.TenantId == tenantId &&
                c.OrgId == caseEntity.OrgId &&
                c.IsActive &&
                c.ContactType == ContactType.LawFirm &&
                c.ContactSubtype == null)
            .OrderBy(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return FirstNonEmpty(defaultLawFirm?.Organization, defaultLawFirm?.DisplayName);
    }

    private static async Task<string?> ResolveCaseManagerAsync(
        LiensDbContext db,
        Guid tenantId,
        Case? caseEntity,
        CancellationToken ct)
    {
        if (caseEntity is null)
            return null;

        var metadata = ParseLegacyNoteFields(caseEntity.Notes);
        if (!Guid.TryParse(metadata.GetValueOrDefault("caseManagerId"), out var caseManagerId))
            return null;

        var caseManager = await db.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == caseManagerId, ct);

        return FirstNonEmpty(caseManager?.DisplayName);
    }

    private static async Task<IReadOnlyList<PublicDocumentView>> ResolveDocumentsAsync(
        LiensDbContext db,
        Guid tenantId,
        Lien lien,
        Case? caseEntity,
        CancellationToken ct)
    {
        var caseId = caseEntity?.Id;
        var query = db.ServicingItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

        query = caseId.HasValue
            ? query.Where(item => item.LienId == lien.Id || item.CaseId == caseId.Value)
            : query.Where(item => item.LienId == lien.Id);

        var items = await query
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(ct);

        return items
            .Where(item => DocumentTaskTypes.Contains(item.TaskType, StringComparer.Ordinal))
            .Select(MapDocument)
            .Where(document => !string.IsNullOrWhiteSpace(document.FileName))
            .DistinctBy(document => document.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static PublicDocumentView MapDocument(ServicingItem item)
    {
        var fields = ParseLegacyNoteFields(item.Notes);
        var fileName = FirstNonEmpty(
            fields.GetValueOrDefault("originalFileName"),
            fields.GetValueOrDefault("filename"),
            item.Description) ?? string.Empty;

        var category = FirstNonEmpty(
            fields.GetValueOrDefault("documentCategory"),
            fields.GetValueOrDefault("category"),
            HumanizeDocumentTaskType(item.TaskType));

        var size = FirstNonEmpty(
            fields.GetValueOrDefault("size"),
            fields.GetValueOrDefault("fileSize"),
            fields.GetValueOrDefault("contentLength"),
            ResolveFileExtension(fileName));

        return new PublicDocumentView(fileName.Trim(), category, FormatDocumentSize(size));
    }

    private static PublicBuyerPortalResponse MapPublicPortalResponse(PublicPortalView view)
        => new(
            new PublicBuyerAccessLinkResponse(
                view.AccessLink.CreatedAtUtc,
                view.AccessLink.ExpiresAtUtc,
                view.AccessLink.LastAccessedAtUtc,
                view.AccessLink.NotificationSubmittedAtUtc,
                view.AccessLink.ResponseStatus,
                view.AccessLink.ResponseAmount,
                view.AccessLink.ResponseNotes,
                view.AccessLink.RespondedAtUtc),
            new PublicBuyerLienResponse(
                view.Lien.Id,
                ResolveLienCode(view.Lien),
                view.Lien.Status,
                view.Lien.SellerStatus,
                view.Lien.SubmittedForSaleAtUtc ?? view.AccessLink.CreatedAtUtc,
                view.Lien.ListingVisibility,
                view.Lien.InitialServiceDate,
                view.Lien.EndServiceDate,
                view.Lien.OriginalAmount,
                view.Lien.AskAmount,
                view.Lien.OfferPrice,
                FirstNonEmpty(view.Lien.Description, view.Lien.Notes)),
            new PublicBuyerSellerResponse(
                view.SellerContact?.DisplayName,
                view.SellerContact?.Organization,
                view.SellerContact?.Email),
            new PublicBuyerOrganizationResponse(
                view.BuyerContact?.DisplayName,
                view.BuyerContact?.Organization,
                view.BuyerContact?.Email),
            new PublicBuyerCaseResponse(
                view.HandlingLawFirm,
                view.CaseManager),
            view.Documents
                .Select(document => new PublicBuyerDocumentResponse(
                    document.FileName,
                    document.Category,
                    document.SizeOrType))
                .ToList());

    private static IResult PublicLinkState(string code, string title, string message, int statusCode)
        => Results.Json(
            new PublicBuyerPortalErrorResponse(new PublicBuyerPortalError(code, title, message)),
            statusCode: statusCode);

    private static Contact? SelectSellerContact(IReadOnlyList<Contact> contacts)
        => contacts.FirstOrDefault(c =>
               string.Equals(c.ContactType, ContactType.LawFirm, StringComparison.Ordinal) &&
               string.IsNullOrWhiteSpace(c.ContactSubtype) &&
               !string.IsNullOrWhiteSpace(c.Email))
           ?? contacts.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Email))
           ?? contacts.FirstOrDefault();

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        foreach (var segment in notes.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = value;
        }

        return result;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string ResolveLienCode(Lien lien)
        => string.IsNullOrWhiteSpace(lien.LienNumber) ? lien.Id.ToString() : lien.LienNumber;

    private static string FormatDocumentSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (!long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
            return trimmed;

        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024m * 1024m):0.#} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024m:0.#} KB";

        return $"{bytes} B";
    }

    private static string? ResolveFileExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(extension)
            ? null
            : extension.TrimStart('.').ToUpperInvariant();
    }

    private static string HumanizeDocumentTaskType(string taskType)
        => taskType switch
        {
            "LegacyCaseDocument" => "Case Document",
            "LegacyLienDocument" => "Lien Document",
            "LegacyMedicalDocument" => "Medical Document",
            _ => "Document",
        };

    private static bool IsActionableLienStatus(string status)
        => string.Equals(status, LienStatus.Offered, StringComparison.Ordinal)
           || string.Equals(status, LienStatus.UnderReview, StringComparison.Ordinal);

    private static string? ReadIdempotencyKey(HttpContext httpContext)
        => httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();

    private sealed record PublicPortalView(
        SellingBuyerAccessLink AccessLink,
        Lien Lien,
        Case? Case,
        Contact? BuyerContact,
        Contact? SellerContact,
        string? HandlingLawFirm,
        string? CaseManager,
        IReadOnlyList<PublicDocumentView> Documents);

    private sealed record PublicDocumentView(string FileName, string? Category, string SizeOrType);

    private sealed record PublicBuyerPortalResponse(
        PublicBuyerAccessLinkResponse AccessLink,
        PublicBuyerLienResponse Lien,
        PublicBuyerSellerResponse Seller,
        PublicBuyerOrganizationResponse Buyer,
        PublicBuyerCaseResponse Case,
        IReadOnlyList<PublicBuyerDocumentResponse> Documents);

    private sealed record PublicBuyerAccessLinkResponse(
        DateTime CreatedAtUtc,
        DateTime ExpiresAtUtc,
        DateTime? LastAccessedAtUtc,
        DateTime? NotificationSubmittedAtUtc,
        string? ResponseStatus,
        decimal? ResponseAmount,
        string? ResponseNotes,
        DateTime? RespondedAtUtc);

    private sealed record PublicBuyerLienResponse(
        Guid Id,
        string LienCode,
        string Status,
        string? SellerStatus,
        DateTime SubmittedAtUtc,
        string? ListingVisibility,
        DateOnly? InitialServiceDate,
        DateOnly? EndServiceDate,
        decimal OriginalAmount,
        decimal? AskAmount,
        decimal? OfferPrice,
        string? Notes);

    private sealed record PublicBuyerSellerResponse(
        string? Name,
        string? Company,
        string? Email);

    private sealed record PublicBuyerOrganizationResponse(
        string? ContactName,
        string? Company,
        string? Email);

    private sealed record PublicBuyerCaseResponse(
        string? HandlingLawFirm,
        string? CaseManager);

    private sealed record PublicBuyerDocumentResponse(
        string FileName,
        string? Category,
        string SizeOrType);

    private sealed record PublicBuyerPortalErrorResponse(PublicBuyerPortalError Error);

    private sealed record PublicBuyerPortalError(string Code, string Title, string Message);

    private sealed record PublicBuyerAcceptLienRequest(string? Notes, string? Message);

    private sealed record PublicBuyerDeclineLienRequest(string? Reason);
}
