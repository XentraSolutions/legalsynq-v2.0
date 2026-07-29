using System.Globalization;
using Liens.Application.Interfaces;
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

        group.MapPost("/{token}/offers", SubmitPublicBuyerOffer)
            .AllowAnonymous();

        group.MapPost("/{token}/decline", DeclineTemporaryBuyerPortal)
            .AllowAnonymous();

        group.MapPost("/{token}/activate-account", ActivateBuyerAccount)
            .AllowAnonymous();
    }

    private static async Task<IResult> GetTemporaryBuyerPortal(
        string token,
        HttpResponse response,
        LiensDbContext db,
        CancellationToken ct = default)
    {
        SetNoReferrerHeader(response);
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
        HttpRequest httpRequest,
        HttpResponse response,
        LiensDbContext db,
        CancellationToken ct = default)
    {
        SetNoReferrerHeader(response);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError))
            return idempotencyError!;
        var resolved = await ResolvePublicAccessLinkAsync(token, db, ct);
        if (resolved.Error is not null)
            return resolved.Error;

        var replay = await SellingIdempotency.GetReplayAsync(
            db,
            resolved.AccessLink!.TenantId,
            "BuyerAccessLink",
            resolved.AccessLink.Id,
            "/api/liens/selling/public/{token}/accept",
            "Lien",
            resolved.AccessLink.LienId.ToString(),
            idempotencyKey!,
            request,
            ct);
        if (replay is not null)
            return replay;

        if (!string.IsNullOrWhiteSpace(resolved.AccessLink.ResponseStatus))
        {
            return PublicLinkState(
                "response-conflict",
                "Lien response already recorded",
                "A buyer response has already been recorded for this secure link.",
                StatusCodes.Status409Conflict);
        }

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

        if (!string.IsNullOrWhiteSpace(view.AccessLink.ResponseStatus))
        {
            return PublicLinkState(
                "response-conflict",
                "Lien response already recorded",
                "A buyer response has already been recorded for this secure link.",
                StatusCodes.Status409Conflict);
        }

        var lienTransition = await SellingIdempotency.TryBeginAsync(
            db,
            view.AccessLink.TenantId,
            "LienStateTransition",
            view.Lien.Id,
            "/api/liens/selling/liens/{lienId}/state-transition",
            "Lien",
            view.Lien.Id.ToString(),
            "lien-state-transition-v1",
            request: null,
            ct: ct);
        if (lienTransition.Result is not null)
        {
            return PublicLinkState(
                "not-actionable",
                "Lien offer unavailable",
                "This lien is changing state and cannot accept a buyer response.",
                StatusCodes.Status409Conflict);
        }

        var responseTransition = await SellingIdempotency.TryBeginAsync(
            db,
            view.AccessLink.TenantId,
            "BuyerLinkResponseTransition",
            view.AccessLink.Id,
            "/api/liens/selling/public/{token}/response",
            "BuyerAccessLink",
            view.AccessLink.Id.ToString(),
            "buyer-response-transition-v1",
            request: null,
            ct: ct);
        if (responseTransition.Result is not null)
        {
            db.SellingIdempotencyRecords.Remove(lienTransition.Record!);
            await db.SaveChangesAsync(ct);
            return PublicLinkState(
                "response-conflict",
                "Lien response already recorded",
                "A buyer response is already being recorded for this secure link.",
                StatusCodes.Status409Conflict);
        }

        var started = await SellingIdempotency.TryBeginAsync(
            db,
            view.AccessLink.TenantId,
            "BuyerAccessLink",
            view.AccessLink.Id,
            "/api/liens/selling/public/{token}/accept",
            "Lien",
            view.Lien.Id.ToString(),
            idempotencyKey!,
            request,
            ct);
        if (started.Result is not null)
        {
            db.SellingIdempotencyRecords.Remove(responseTransition.Record!);
            db.SellingIdempotencyRecords.Remove(lienTransition.Record!);
            await db.SaveChangesAsync(ct);
            return started.Result;
        }

        view.AccessLink.MarkAccessed();
        view.AccessLink.RecordResponse(
            SellingBuyerResponseStatus.Accepted,
            responseAmount.Value,
            FirstNonEmpty(request?.Notes, request?.Message));
        await ApplyPublicResponseToLienAsync(db, view, SellingBuyerResponseStatus.Accepted, ct);
        await db.SaveChangesAsync(ct);

        var persistedLien = await db.Liens.AsNoTracking().FirstAsync(
            lien => lien.TenantId == view.AccessLink.TenantId && lien.Id == view.Lien.Id,
            ct);
        // Accepted links are intentionally no longer actionable, so the public
        // projection builder returns null. Use the post-transition lien for the
        // immediate response rather than leaking the stale Offered state.
        var updatedView = await BuildPublicViewAsync(db, view.AccessLink, ct) ?? view with { Lien = persistedLien };
        var completed = await SellingIdempotency.CompleteAsync(
            db,
            started.Record!,
            ResolvePublicResponseActorId(view.AccessLink),
            StatusCodes.Status200OK,
            MapPublicPortalResponse(updatedView),
            ct);
        await SellingIdempotency.CompleteAsync(
            db,
            responseTransition.Record!,
            ResolvePublicResponseActorId(view.AccessLink),
            StatusCodes.Status200OK,
            MapPublicPortalResponse(updatedView),
            ct);
        await SellingIdempotency.CompleteAsync(
            db,
            lienTransition.Record!,
            ResolvePublicResponseActorId(view.AccessLink),
            StatusCodes.Status200OK,
            MapPublicPortalResponse(updatedView),
            ct);
        return completed;
    }

    private static async Task<IResult> DeclineTemporaryBuyerPortal(
        string token,
        PublicBuyerDeclineLienRequest? request,
        HttpRequest httpRequest,
        HttpResponse response,
        LiensDbContext db,
        CancellationToken ct = default)
    {
        SetNoReferrerHeader(response);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError))
            return idempotencyError!;
        var resolved = await ResolvePublicAccessLinkAsync(token, db, ct);
        if (resolved.Error is not null)
            return resolved.Error;

        var replay = await SellingIdempotency.GetReplayAsync(
            db,
            resolved.AccessLink!.TenantId,
            "BuyerAccessLink",
            resolved.AccessLink.Id,
            "/api/liens/selling/public/{token}/decline",
            "Lien",
            resolved.AccessLink.LienId.ToString(),
            idempotencyKey!,
            request,
            ct);
        if (replay is not null)
            return replay;

        if (!string.IsNullOrWhiteSpace(resolved.AccessLink.ResponseStatus))
        {
            return PublicLinkState(
                "response-conflict",
                "Lien response already recorded",
                "A buyer response has already been recorded for this secure link.",
                StatusCodes.Status409Conflict);
        }

        var view = await BuildPublicViewAsync(db, resolved.AccessLink!, ct);
        if (view is null)
        {
            return PublicLinkState(
                "unavailable",
                "Lien offer unavailable",
                "The lien offer data could not be resolved.",
                StatusCodes.Status404NotFound);
        }

        if (!IsActionableLien(view.Lien))
        {
            return PublicLinkState(
                "not-actionable",
                "Lien offer unavailable",
                "This lien is no longer accepting buyer responses.",
                StatusCodes.Status409Conflict);
        }

        var responseTransition = await SellingIdempotency.TryBeginAsync(
            db,
            view.AccessLink.TenantId,
            "BuyerLinkResponseTransition",
            view.AccessLink.Id,
            "/api/liens/selling/public/{token}/response",
            "BuyerAccessLink",
            view.AccessLink.Id.ToString(),
            "buyer-response-transition-v1",
            request: null,
            ct: ct);
        if (responseTransition.Result is not null)
        {
            return PublicLinkState(
                "response-conflict",
                "Lien response already recorded",
                "A buyer response is already being recorded for this secure link.",
                StatusCodes.Status409Conflict);
        }

        var started = await SellingIdempotency.TryBeginAsync(
            db,
            view.AccessLink.TenantId,
            "BuyerAccessLink",
            view.AccessLink.Id,
            "/api/liens/selling/public/{token}/decline",
            "Lien",
            view.Lien.Id.ToString(),
            idempotencyKey!,
            request,
            ct);
        if (started.Result is not null)
        {
            db.SellingIdempotencyRecords.Remove(responseTransition.Record!);
            await db.SaveChangesAsync(ct);
            return started.Result;
        }
        view.AccessLink.MarkAccessed();
        view.AccessLink.RecordResponse(SellingBuyerResponseStatus.Declined, null, request?.Reason);
        await db.SaveChangesAsync(ct);
        var completed = await SellingIdempotency.CompleteAsync(
            db,
            started.Record!,
            view.AccessLink.BuyerContactId,
            StatusCodes.Status200OK,
            MapPublicPortalResponse(view),
            ct);
        await SellingIdempotency.CompleteAsync(
            db,
            responseTransition.Record!,
            view.AccessLink.BuyerContactId,
            StatusCodes.Status200OK,
            MapPublicPortalResponse(view),
            ct);
        return completed;
    }

    private static async Task<IResult> SubmitPublicBuyerOffer(
        string token,
        PublicBuyerOfferRequest? request,
        HttpRequest httpRequest,
        HttpResponse response,
        LiensDbContext db,
        CancellationToken ct = default)
    {
        SetNoReferrerHeader(response);
        if (!SellingIdempotency.TryGetKey(httpRequest, out var idempotencyKey, out var idempotencyError))
            return idempotencyError!;
        if (request is null || request.OfferAmount <= 0m)
        {
            return PublicLinkState(
                "invalid-offer",
                "Lien offer unavailable",
                "offerAmount must be positive.",
                StatusCodes.Status400BadRequest);
        }

        var resolved = await ResolvePublicAccessLinkAsync(token, db, ct);
        if (resolved.Error is not null)
            return resolved.Error;

        var replay = await SellingIdempotency.GetReplayAsync(
            db,
            resolved.AccessLink!.TenantId,
            "BuyerAccessLink",
            resolved.AccessLink.Id,
            "/api/liens/selling/public/{token}/offers",
            "Lien",
            resolved.AccessLink.LienId.ToString(),
            idempotencyKey!,
            request,
            ct);
        if (replay is not null)
            return replay;

        if (!string.IsNullOrWhiteSpace(resolved.AccessLink.ResponseStatus))
        {
            return PublicLinkState(
                "response-conflict",
                "Lien response already recorded",
                "A buyer response has already been recorded for this secure link.",
                StatusCodes.Status409Conflict);
        }

        var view = await BuildPublicViewAsync(db, resolved.AccessLink!, ct);
        if (view is null || !IsActionableLien(view.Lien))
        {
            return PublicLinkState(
                "not-actionable",
                "Lien offer unavailable",
                "This lien is no longer accepting buyer offers.",
                StatusCodes.Status409Conflict);
        }

        var activeOfferExists = await db.LienOffers.AnyAsync(offer =>
            offer.TenantId == view.AccessLink.TenantId &&
            offer.LienId == view.Lien.Id &&
            offer.BuyerOrgId == view.AccessLink.BuyerOrgId &&
            offer.Status == OfferStatus.Pending &&
            (!offer.ExpiresAtUtc.HasValue || offer.ExpiresAtUtc > DateTime.UtcNow),
            ct);
        if (activeOfferExists)
        {
            return PublicLinkState(
                "active-offer-exists",
                "Lien offer unavailable",
                "An active offer has already been submitted by this buyer organization.",
                StatusCodes.Status409Conflict);
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        try
        {
            var started = await SellingIdempotency.TryBeginAsync(
                db,
                view.AccessLink.TenantId,
                "BuyerAccessLink",
                view.AccessLink.Id,
                "/api/liens/selling/public/{token}/offers",
                "Lien",
                view.Lien.Id.ToString(),
                idempotencyKey!,
                request,
                ct);
            if (started.Result is not null)
                return started.Result;

            // Repeat the predicate inside a serializable transaction after the
            // idempotency row is reserved. This closes the different-key race
            // that could otherwise create two active offers for one buyer/lien.
            activeOfferExists = await db.LienOffers.AnyAsync(offer =>
                offer.TenantId == view.AccessLink.TenantId &&
                offer.LienId == view.Lien.Id &&
                offer.BuyerOrgId == view.AccessLink.BuyerOrgId &&
                offer.Status == OfferStatus.Pending &&
                (!offer.ExpiresAtUtc.HasValue || offer.ExpiresAtUtc > DateTime.UtcNow),
                ct);
            if (activeOfferExists)
            {
                var conflict = await SellingIdempotency.CompleteAsync(
                    db,
                    started.Record!,
                    view.AccessLink.BuyerContactId,
                    StatusCodes.Status409Conflict,
                    new { error = new { code = "active_offer_exists", message = "An active offer has already been submitted by this buyer organization." } },
                    ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return conflict;
            }

            var offer = LienOffer.Create(
                view.AccessLink.TenantId,
                view.Lien.Id,
                view.AccessLink.BuyerOrgId,
                view.AccessLink.SellerOrgId,
                request.OfferAmount,
                view.AccessLink.BuyerContactId,
                request.Message);
            db.LienOffers.Add(offer);
            view.AccessLink.MarkAccessed();
            await db.SaveChangesAsync(ct);
            var completed = await SellingIdempotency.CompleteAsync(db, started.Record!, view.AccessLink.BuyerContactId, StatusCodes.Status201Created, new
            {
                offer.Id,
                offer.LienId,
                offer.OfferAmount,
                offer.Status,
                offer.OfferedAtUtc,
            }, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return completed;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<IResult> ActivateBuyerAccount(
        string token,
        PublicBuyerActivateAccountRequest? request,
        IPublicBuyerAccountProvisioningService provisioningService,
        HttpResponse response,
        LiensDbContext db,
        CancellationToken ct = default)
    {
        SetNoReferrerHeader(response);
        if (request is null)
        {
            return PublicLinkState(
                "invalid-activation-request",
                "Account activation failed",
                "The account activation request is missing.",
                StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return PublicLinkState(
                "password-required",
                "Account activation failed",
                "Password is required.",
                StatusCodes.Status400BadRequest);
        }

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

        var email = FirstNonEmpty(view.BuyerContact?.Email, request.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return PublicLinkState(
                "buyer-email-required",
                "Account activation failed",
                "This lien offer does not have a buyer email address.",
                StatusCodes.Status409Conflict);
        }

        var buyerCompanyName = FirstNonEmpty(view.BuyerContact?.Organization, request.CompanyName);
        if (string.IsNullOrWhiteSpace(buyerCompanyName))
        {
            return PublicLinkState(
                "buyer-company-required",
                "Account activation failed",
                "Company name is required to activate a buyer account.",
                StatusCodes.Status400BadRequest);
        }

        var nameParts = SplitName(view.BuyerContact?.DisplayName);
        var firstName = FirstNonEmpty(view.BuyerContact?.FirstName, nameParts.FirstName, request.FirstName);
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return PublicLinkState(
                "first-name-required",
                "Account activation failed",
                "First name is required.",
                StatusCodes.Status400BadRequest);
        }

        var result = await provisioningService.ProvisionBuyerAccountAsync(
            new PublicBuyerAccountProvisioningRequest(
                view.AccessLink.TenantId,
                view.AccessLink.BuyerOrgId,
                buyerCompanyName,
                email,
                request.Password.Trim(),
                firstName,
                FirstNonEmpty(view.BuyerContact?.LastName, nameParts.LastName, request.LastName),
                NormalizePhoneForIdentity(FirstNonEmpty(view.BuyerContact?.Phone, request.Phone))),
            ct);

        if (!result.Success)
        {
            return PublicLinkState(
                FirstNonEmpty(result.ErrorCode, "activation-failed")!,
                "Account activation failed",
                FirstNonEmpty(result.ErrorMessage, "Account activation could not be completed.")!,
                result.StatusCode.GetValueOrDefault(StatusCodes.Status503ServiceUnavailable));
        }

        view.AccessLink.MarkAccessed();
        await db.SaveChangesAsync(ct);

        return Results.Ok(new PublicBuyerAccountActivationResponse(
            result.UserId!.Value,
            result.IsNew,
            "/login?returnTo=%2Ffunding%2Foffered-liens&reason=synqlien-buyer-activation"));
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

        var tokenHash = SellingBuyerAccessLink.ComputeTokenHash(token);
        var accessLink = await db.SellingBuyerAccessLinks
            .FirstOrDefaultAsync(link => link.TokenHash == tokenHash, ct);

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
            responseNotes);

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

        if (!IsActionableLien(lien))
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
                view.Lien.OfferPrice),
            new PublicBuyerSellerResponse(
                view.SellerContact?.DisplayName,
                view.SellerContact?.Organization,
                view.SellerContact?.Email),
            new PublicBuyerOrganizationResponse(
                view.BuyerContact?.Organization),
            new PublicBuyerCaseResponse(
                view.HandlingLawFirm,
                view.CaseManager),
            []);

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

    private static string? NormalizePhoneForIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return trimmed;

        if (trimmed.StartsWith('+'))
            return "+" + digits;

        return digits.Length switch
        {
            10 => "+1" + digits,
            11 when digits.StartsWith('1') => "+" + digits,
            _ => trimmed,
        };
    }

    private static (string? FirstName, string? LastName) SplitName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (null, null);

        var parts = value.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            _ => (parts[0], string.Join(' ', parts.Skip(1))),
        };
    }

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

    private static bool HasIdempotencyKey(HttpRequest request) =>
        !string.IsNullOrWhiteSpace(request.Headers["Idempotency-Key"].FirstOrDefault());

    private static bool IsActionableLien(Lien lien)
        => IsActionableLienStatus(lien.Status) &&
           string.Equals(lien.SellerStatus, SellingLienStatus.SubmittedForSale, StringComparison.Ordinal) &&
           !lien.ArchivedAtUtc.HasValue &&
           !lien.WithdrawnAtUtc.HasValue &&
           !lien.SoldAtUtc.HasValue;

    private static void SetNoReferrerHeader(HttpResponse response) =>
        response.Headers["Referrer-Policy"] = "no-referrer";

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
        decimal? OfferPrice);

    private sealed record PublicBuyerSellerResponse(
        string? Name,
        string? Company,
        string? Email);

    private sealed record PublicBuyerOrganizationResponse(string? Company);

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

    private sealed record PublicBuyerOfferRequest(decimal OfferAmount, string? Message);

    private sealed record PublicBuyerDeclineLienRequest(string? Reason);

    private sealed record PublicBuyerActivateAccountRequest(
        string? CompanyName,
        string? Email,
        string Password,
        string? FirstName,
        string? LastName,
        string? Phone);

    private sealed record PublicBuyerAccountActivationResponse(
        Guid UserId,
        bool IsNew,
        string LoginUrl);
}
