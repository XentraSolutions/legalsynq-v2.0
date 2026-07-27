using System.Globalization;
using System.Net;
using System.Text;
using BuildingBlocks.Notifications;
using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Liens.Api.Endpoints;

public static class SellingPublicEndpoints
{
    private const int MaxPublicMessageLength = 400;
    private const string LegalSynqBrandIconContentId = "legalsynq-brand-icon";
    private const string LegalSynqBrandIconPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAHgAAAB4CAYAAAA5ZDbSAAAFH0lEQVR42u2d23HiMBSGU4JLcAmOSWZ4pIR0sDxsAYLdfQ4dLB2QDrYEtgOX4AZCVILXwmJyWRsdCev+/zPnFbA+jq7Wf+7uIAiCvIqzVcG39fq0XRzeNnXbR0cI/rZZHE/bmnG2LNGKocLd1s8DLBLUyRB/DoAOKmuXZZ+Bza1gv2a1yGi0bhBwyV2xdoheAa2cKNz3LhuZ7EViYmQb7qW7xpjsWK/9TNkRXBmLI1rdbfY2bgH34zF7rNDy7sZe9djJ6p2qa+WsKjg79wYtYSzeofUdqAfHVDBee2h6f5qKMGFDN+0G8KbeK5Y2B7Oe4WGlmmyh9QOYPZsua0R3rR6HqwIEPAMWM2zzz1YCxnIJgCEAhgA4610ssSadils2JAA4+d4BgAEYAmAIgPNS13VFH099sD52DuLJBHCvveJzxe9fiecB1QHsuo9j514HQ8A6Es+1zhWs+Je3nT+5AHyReM4yJ7jPnX+5BHzRcw5wD10Y8gE4bciBZK5vwEIs1TG3A+CzeHJjsucJVWiAzzPs1JZCOksL1Voz1HXwXnPJt0oF8JEI1nu3NcdOlngOYo+1T2WHSqUmta1KIuQ2l8lVmRpgjWcvYge8jmmyMfdhg5wxX1MVO2BmMptNCHCT9ERLzi5zBnwE4LQBV3IsnooCgCMGnMPhAgADMAADMADnA/hsdsaWpY1QAv75sLL13apwAoz/eqwGJ7ma/Xej4MfiyQVg9/4cQcXZlU+0wWzQpT2g0kVu7OI1AFuN9mYfLx17QAD2CFo3m0XW6vpQAbBvyMRLdxJuY2D7B8AxZLK04u0AOMZQOAHd0ngAHEaIpdy1hX4LwIlmMcEn6oOX8uLwNfiI+VhsO1khS+yiCfc+mu3iyI4bZewVJmQ6XlEAbAO0ugcb9SlRzZxNHOQA2NaeucIMrk/EsfGXGw/eAOxUKr/O0WS0cZKSO2B5qX19JYze6FBNNAHYHWAr72QBMAADMAADMAADMABfXHSmggFwxIBzly3ARmUNbGx0ALAdwKpDodEKMeo3OBYNkPkFLN+POxgdGarS/nIUhTJv9gGLN1TF26sf47R9+E08zuUT/w7ycWE3HEyIjM8rXN0unPtcXqObzvxtiRgAX/uNelkMwKEBJpXfo741AMBhAT5t7l/oC+nN4gVA4wGsBReZHBVgLg7/jaf51NKrAOwcMJfvx82zZB1AY4btCnA/RO7H3lwd6kKJK6wWi2SKOzDDl/gJ9dBx/9fWd4eyVZm0jE5SEtuLBmAABmAABmAABuDMAfu4XQjAbh9wnTngJnfAPFXA0vVdpej9oikPyRIFrKwTlcSkiOB6znX+yTEA7p/nWzaldWS5GVLZN0r9hpABSw9oavm+dSqAZ696ZgI4sMJcaVUk7R/mBYA/aZfU5oSsn9QCcCL1kiYauCJMuFIHnFbXbAtypIDThvtlbdxmBvjY5VTm/cM+dZs4YJ76FVnqduYfHdiBA27lWnh1B0124ZPRfK+YIeDSdoDeDIrpNAkCYAiAARiAAZimsZv1nwLuBnEDVl3PGfVhhgAYAmAIgAEYgHMAPOkTRQJ8/eI13P4cSNxPJrjNMP3PPRfihF9nCKJURBWVUynrVmkP+JtSGxAt70g028V3MIq43UEOmrubrsrQ/Tkgt1l8U5Ac5KDZs7hwZPGEsddzV20TcouuOVnI+vbBkM0xeT7rRWEPiDE31Gw2dOXjQ8bWzKqLHDQ38GV53b1OuPbhEB+CoND1D6mLXlFVwRdjAAAAAElFTkSuQmCC";

    private static readonly IReadOnlyList<NotificationEmailInlineAttachment> PublicResponseInlineAttachments =
    [
        new(
            LegalSynqBrandIconContentId,
            "legalsynq-brand-icon.png",
            "image/png",
            LegalSynqBrandIconPngBase64),
    ];

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

        group.MapPost("/{token}/messages", PostTemporaryBuyerPortalMessage)
            .AllowAnonymous();

        group.MapPost("/{token}/accept", AcceptTemporaryBuyerPortal)
            .AllowAnonymous();

        group.MapPost("/{token}/offers", AcceptTemporaryBuyerPortal)
            .AllowAnonymous();

        group.MapPost("/{token}/decline", DeclineTemporaryBuyerPortal)
            .AllowAnonymous();

        group.MapPost("/{token}/activate-account", ActivateBuyerAccount)
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

    private static async Task<IResult> PostTemporaryBuyerPortalMessage(
        string token,
        PublicPortalMessageRequest? request,
        HttpContext httpContext,
        INotificationPublisher notifications,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        LiensDbContext db,
        CancellationToken ct = default)
    {
        var messageText = request?.Message?.Trim() ?? string.Empty;
        if (messageText.Length == 0)
        {
            return PublicLinkState(
                "message-required",
                "Message could not be sent",
                "Enter a message before sending.",
                StatusCodes.Status400BadRequest);
        }

        if (messageText.Length > MaxPublicMessageLength)
        {
            return PublicLinkState(
                "message-too-long",
                "Message could not be sent",
                $"Message must be {MaxPublicMessageLength} characters or fewer.",
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

        var senderType = ResolvePublicAudience(view.AccessLink);
        var sender = ResolvePublicMessageSender(view, senderType);
        var publicMessage = SellingPortalMessage.Create(
            view.AccessLink.TenantId,
            view.AccessLink.LienId,
            view.AccessLink.SellerOrgId,
            view.AccessLink.BuyerOrgId,
            view.AccessLink.BuyerContactId,
            view.AccessLink.Id,
            senderType,
            sender.Name,
            sender.Email,
            messageText,
            ResolvePublicMessageActorId(view.AccessLink, senderType));

        view.AccessLink.MarkAccessed();
        db.SellingPortalMessages.Add(publicMessage);
        await db.SaveChangesAsync(ct);

        await SendPublicMessageNotificationAsync(
            notifications,
            loggerFactory,
            configuration,
            httpContext,
            db,
            view,
            publicMessage,
            ct);

        return Results.Created(
            $"/api/liens/selling/public/{Uri.EscapeDataString(token)}/messages/{publicMessage.Id}",
            MapPublicMessage(publicMessage));
    }

    private static async Task<IResult> AcceptTemporaryBuyerPortal(
        string token,
        PublicBuyerAcceptLienRequest? request,
        HttpContext httpContext,
        INotificationPublisher notifications,
        ILoggerFactory loggerFactory,
        LiensDbContext db,
        CancellationToken ct = default)
    {
        var resolved = await ResolvePublicAccessLinkAsync(token, db, ct);
        if (resolved.Error is not null)
            return resolved.Error;
        if (EnsureBuyerResponseLink(resolved.AccessLink!) is { } readOnlyError)
            return readOnlyError;

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
            notifications,
            loggerFactory,
            ct);
    }

    private static async Task<IResult> DeclineTemporaryBuyerPortal(
        string token,
        PublicBuyerDeclineLienRequest? request,
        HttpContext httpContext,
        INotificationPublisher notifications,
        ILoggerFactory loggerFactory,
        LiensDbContext db,
        CancellationToken ct = default)
    {
        var resolved = await ResolvePublicAccessLinkAsync(token, db, ct);
        if (resolved.Error is not null)
            return resolved.Error;
        if (EnsureBuyerResponseLink(resolved.AccessLink!) is { } readOnlyError)
            return readOnlyError;

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
            notifications,
            loggerFactory,
            ct);
    }

    private static async Task<IResult> ActivateBuyerAccount(
        string token,
        PublicBuyerActivateAccountRequest? request,
        IPublicBuyerAccountProvisioningService provisioningService,
        LiensDbContext db,
        CancellationToken ct = default)
    {
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
        if (EnsureBuyerResponseLink(resolved.AccessLink!) is { } readOnlyError)
            return readOnlyError;

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

        if (!IsSupportedPublicPurpose(accessLink.Purpose))
        {
            return (null, PublicLinkState(
                "not-found",
                "Lien offer link unavailable",
                "The secure link could not be found.",
                StatusCodes.Status404NotFound));
        }

        return (accessLink, null);
    }

    private static IResult? EnsureBuyerResponseLink(SellingBuyerAccessLink accessLink)
        => IsBuyerResponseLink(accessLink)
            ? null
            : PublicLinkState(
                "read-only-link",
                "Lien offer is read-only",
                "This secure link is for viewing lien details and cannot record buyer responses.",
                StatusCodes.Status403Forbidden);

    private static async Task<IResult> RecordPublicResponseAsync(
        LiensDbContext db,
        PublicPortalView view,
        string responseStatus,
        decimal? responseAmount,
        string? responseNotes,
        string? responseIdempotencyKey,
        INotificationPublisher notifications,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(view.AccessLink.ResponseStatus))
        {
            if (string.Equals(view.AccessLink.ResponseStatus, responseStatus, StringComparison.Ordinal))
            {
                await ApplyPublicResponseToLienAsync(db, view, responseStatus, ct);
                await db.SaveChangesAsync(ct);

                var reconciledView = await BuildPublicViewAsync(db, view.AccessLink, ct) ?? view;
                await SendPublicResponseNotificationsAsync(
                    notifications,
                    loggerFactory,
                    reconciledView,
                    responseStatus,
                    reconciledView.AccessLink.ResponseNotes,
                    ct);

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
        await SendPublicResponseNotificationsAsync(
            notifications,
            loggerFactory,
            updatedView,
            responseStatus,
            responseNotes,
            ct);

        return Results.Ok(MapPublicPortalResponse(updatedView));
    }

    private static async Task SendPublicResponseNotificationsAsync(
        INotificationPublisher notifications,
        ILoggerFactory loggerFactory,
        PublicPortalView view,
        string responseStatus,
        string? responseNotes,
        CancellationToken ct)
    {
        var eventKey = string.Equals(responseStatus, SellingBuyerResponseStatus.Accepted, StringComparison.Ordinal)
            ? NotificationTaxonomy.Liens.Events.OfferAccepted
            : NotificationTaxonomy.Liens.Events.OfferRejected;
        var statusLabel = string.Equals(responseStatus, SellingBuyerResponseStatus.Accepted, StringComparison.Ordinal)
            ? "Accepted"
            : "Declined";
        var responseVerb = string.Equals(responseStatus, SellingBuyerResponseStatus.Accepted, StringComparison.Ordinal)
            ? "accepted"
            : "declined";
        var lienCode = ResolveLienCode(view.Lien);
        var subject = $"Lien Offer {statusLabel}";
        var respondedAtUtc = view.AccessLink.RespondedAtUtc?.ToString("O", CultureInfo.InvariantCulture)
                             ?? DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        var buyerName = FirstNonEmpty(view.BuyerContact?.DisplayName, view.BuyerContact?.Email, "Buyer")!;
        var buyerCompany = FirstNonEmpty(view.BuyerContact?.Organization, "Funding company")!;
        var sellerName = FirstNonEmpty(view.SellerContact?.DisplayName, view.SellerContact?.Email, "Seller")!;
        var sellerCompany = FirstNonEmpty(view.SellerContact?.Organization, "Seller company")!;

        var commonMetadata = new Dictionary<string, string>
        {
            ["tenantId"] = view.AccessLink.TenantId.ToString(),
            ["lienId"] = view.AccessLink.LienId.ToString(),
            ["lienCode"] = lienCode,
            ["buyerContactId"] = view.AccessLink.BuyerContactId.ToString(),
            ["buyerOrgId"] = view.AccessLink.BuyerOrgId.ToString(),
            ["sellerOrgId"] = view.AccessLink.SellerOrgId.ToString(),
            ["buyerAccessLinkId"] = view.AccessLink.Id.ToString(),
            ["responseStatus"] = responseStatus,
            ["respondedAtUtc"] = respondedAtUtc,
        };

        await SendPublicResponseNotificationAsync(
            notifications,
            loggerFactory,
            eventKey,
            view.AccessLink.TenantId,
            FirstNonEmpty(view.BuyerContact?.Email),
            subject,
            BuildPublicResponseEmailBody(
                recipientRole: "buyer",
                responseVerb,
                lienCode,
                buyerName,
                buyerCompany,
                sellerCompany,
                responseNotes),
            BuildPublicResponseEmailHtmlBody(
                recipientRole: "buyer",
                statusLabel,
                responseVerb,
                lienCode,
                buyerName,
                buyerCompany,
                sellerCompany,
                responseNotes),
            commonMetadata,
            recipientRole: "buyer",
            idempotencyKey: BuildPublicResponseNotificationIdempotencyKey(view.AccessLink, responseStatus, "buyer"),
            requestedBy: ResolvePublicResponseActorId(view.AccessLink).ToString(),
            ct: ct);

        await SendPublicResponseNotificationAsync(
            notifications,
            loggerFactory,
            eventKey,
            view.AccessLink.TenantId,
            FirstNonEmpty(view.SellerContact?.Email),
            subject,
            BuildPublicResponseEmailBody(
                recipientRole: "seller",
                responseVerb,
                lienCode,
                buyerName,
                buyerCompany,
                sellerCompany,
                responseNotes),
            BuildPublicResponseEmailHtmlBody(
                recipientRole: "seller",
                statusLabel,
                responseVerb,
                lienCode,
                buyerName,
                buyerCompany,
                sellerCompany,
                responseNotes),
            commonMetadata,
            recipientRole: "seller",
            idempotencyKey: BuildPublicResponseNotificationIdempotencyKey(view.AccessLink, responseStatus, "seller"),
            requestedBy: ResolvePublicResponseActorId(view.AccessLink).ToString(),
            ct: ct);
    }

    private static async Task SendPublicResponseNotificationAsync(
        INotificationPublisher notifications,
        ILoggerFactory loggerFactory,
        string eventKey,
        Guid tenantId,
        string? recipientEmail,
        string subject,
        string body,
        string htmlBody,
        IReadOnlyDictionary<string, string> commonMetadata,
        string recipientRole,
        string idempotencyKey,
        string requestedBy,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            return;

        var metadata = new Dictionary<string, string>(commonMetadata)
        {
            ["recipientRole"] = recipientRole,
        };

        try
        {
            var result = await notifications.SendEmailAsync(
                eventKey,
                tenantId,
                recipientEmail.Trim(),
                subject,
                body,
                metadata,
                ct,
                new NotificationEmailSendOptions(
                    IdempotencyKey: idempotencyKey,
                    RequestedBy: requestedBy,
                    HtmlBody: htmlBody,
                    TextBody: body,
                    InlineAttachments: PublicResponseInlineAttachments));

            if (!IsNotificationSubmittedStatus(result.Status) || result.BlockedByPolicy || !string.IsNullOrWhiteSpace(result.FailureCategory))
            {
                loggerFactory
                    .CreateLogger("Liens.Api.Endpoints.SellingPublicEndpoints")
                    .LogWarning(
                        "Public lien response notification was not submitted: Tenant={TenantId} Event={EventKey} Role={RecipientRole} Status={Status} FailureCategory={FailureCategory}",
                        tenantId,
                        eventKey,
                        recipientRole,
                        result.Status,
                        result.FailureCategory);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory
                .CreateLogger("Liens.Api.Endpoints.SellingPublicEndpoints")
                .LogWarning(
                    ex,
                    "Public lien response notification failed: Tenant={TenantId} Event={EventKey} Role={RecipientRole}",
                    tenantId,
                    eventKey,
                    recipientRole);
        }
    }

    private static async Task SendPublicMessageNotificationAsync(
        INotificationPublisher notifications,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        HttpContext httpContext,
        LiensDbContext db,
        PublicPortalView view,
        SellingPortalMessage message,
        CancellationToken ct)
    {
        var recipientRole = message.SenderType == SellingPortalMessageSenderType.Buyer
            ? SellingPortalMessageSenderType.Seller
            : SellingPortalMessageSenderType.Buyer;
        var recipient = ResolvePublicMessageRecipient(view, recipientRole);
        if (string.IsNullOrWhiteSpace(recipient.Email))
            return;

        var recipientAccessLink = await ResolvePublicMessageRecipientAccessLinkAsync(db, view.AccessLink, recipientRole, ct);
        var portalUrl = recipientAccessLink is null
            ? null
            : BuildPublicPortalUrl(configuration, httpContext, recipientAccessLink.Token);
        var lienCode = ResolveLienCode(view.Lien);
        var subject = "New message on lien offer";
        var body = BuildPublicMessageEmailBody(message, lienCode, portalUrl);
        var htmlBody = BuildPublicMessageEmailHtmlBody(message, lienCode, portalUrl);
        var metadata = new Dictionary<string, string>
        {
            ["tenantId"] = view.AccessLink.TenantId.ToString(),
            ["lienId"] = view.AccessLink.LienId.ToString(),
            ["lienCode"] = lienCode,
            ["buyerContactId"] = view.AccessLink.BuyerContactId.ToString(),
            ["buyerOrgId"] = view.AccessLink.BuyerOrgId.ToString(),
            ["sellerOrgId"] = view.AccessLink.SellerOrgId.ToString(),
            ["accessLinkId"] = view.AccessLink.Id.ToString(),
            ["messageId"] = message.Id.ToString(),
            ["senderType"] = message.SenderType,
            ["recipientRole"] = recipientRole,
        };

        try
        {
            var result = await notifications.SendEmailAsync(
                NotificationTaxonomy.Liens.Events.OfferMessageCreated,
                view.AccessLink.TenantId,
                recipient.Email.Trim(),
                subject,
                body,
                metadata,
                ct,
                new NotificationEmailSendOptions(
                    IdempotencyKey: BuildPublicMessageNotificationIdempotencyKey(message, recipientRole),
                    RequestedBy: ResolvePublicMessageActorId(view.AccessLink, message.SenderType).ToString(),
                    HtmlBody: htmlBody,
                    TextBody: body,
                    InlineAttachments: PublicResponseInlineAttachments,
                    DisableClickTracking: true));

            if (!IsNotificationSubmittedStatus(result.Status) || result.BlockedByPolicy || !string.IsNullOrWhiteSpace(result.FailureCategory))
            {
                loggerFactory
                    .CreateLogger("Liens.Api.Endpoints.SellingPublicEndpoints")
                    .LogWarning(
                        "Public lien message notification was not submitted: Tenant={TenantId} MessageId={MessageId} Role={RecipientRole} Status={Status} FailureCategory={FailureCategory}",
                        view.AccessLink.TenantId,
                        message.Id,
                        recipientRole,
                        result.Status,
                        result.FailureCategory);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory
                .CreateLogger("Liens.Api.Endpoints.SellingPublicEndpoints")
                .LogWarning(
                    ex,
                    "Public lien message notification failed: Tenant={TenantId} MessageId={MessageId} Role={RecipientRole}",
                    view.AccessLink.TenantId,
                    message.Id,
                    recipientRole);
        }
    }

    private static Task<SellingBuyerAccessLink?> ResolvePublicMessageRecipientAccessLinkAsync(
        LiensDbContext db,
        SellingBuyerAccessLink currentAccessLink,
        string recipientRole,
        CancellationToken ct)
    {
        var purpose = recipientRole == SellingPortalMessageSenderType.Seller
            ? SellingAccessLinkPurposes.ConfirmSaleSellerView
            : SellingAccessLinkPurposes.ConfirmSaleBuyerResponse;

        if (string.Equals(currentAccessLink.Purpose, purpose, StringComparison.Ordinal))
            return Task.FromResult<SellingBuyerAccessLink?>(currentAccessLink);

        return db.SellingBuyerAccessLinks
            .AsNoTracking()
            .Where(link =>
                link.TenantId == currentAccessLink.TenantId &&
                link.LienId == currentAccessLink.LienId &&
                link.SellerOrgId == currentAccessLink.SellerOrgId &&
                link.BuyerOrgId == currentAccessLink.BuyerOrgId &&
                link.BuyerContactId == currentAccessLink.BuyerContactId &&
                link.Purpose == purpose &&
                !link.RevokedAtUtc.HasValue &&
                link.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(link => link.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    private static (string Name, string? Email) ResolvePublicMessageSender(
        PublicPortalView view,
        string senderType)
        => senderType == SellingPortalMessageSenderType.Seller
            ? (
                FirstNonEmpty(view.SellerContact?.DisplayName, view.SellerContact?.Email, view.SellerContact?.Organization, "Seller")!,
                FirstNonEmpty(view.SellerContact?.Email))
            : (
                FirstNonEmpty(view.BuyerContact?.DisplayName, view.BuyerContact?.Email, view.BuyerContact?.Organization, "Buyer")!,
                FirstNonEmpty(view.BuyerContact?.Email));

    private static (string Name, string? Email) ResolvePublicMessageRecipient(
        PublicPortalView view,
        string recipientRole)
        => recipientRole == SellingPortalMessageSenderType.Seller
            ? (
                FirstNonEmpty(view.SellerContact?.DisplayName, view.SellerContact?.Email, view.SellerContact?.Organization, "Seller")!,
                FirstNonEmpty(view.SellerContact?.Email))
            : (
                FirstNonEmpty(view.BuyerContact?.DisplayName, view.BuyerContact?.Email, view.BuyerContact?.Organization, "Buyer")!,
                FirstNonEmpty(view.BuyerContact?.Email));

    private static Guid ResolvePublicMessageActorId(SellingBuyerAccessLink accessLink, string senderType)
        => senderType == SellingPortalMessageSenderType.Buyer
            ? accessLink.BuyerContactId
            : accessLink.CreatedByUserId.GetValueOrDefault(accessLink.BuyerContactId);

    private static string BuildPublicMessageEmailBody(
        SellingPortalMessage message,
        string lienCode,
        string? portalUrl)
    {
        var body = new List<string>
        {
            "LegalSynq",
            "New message on lien offer",
            string.Empty,
            $"{message.SenderName} sent a message regarding lien offer {lienCode}.",
            string.Empty,
            message.Message,
        };

        if (!string.IsNullOrWhiteSpace(portalUrl))
        {
            body.Add(string.Empty);
            body.Add($"View and reply: {portalUrl}");
        }

        return string.Join(Environment.NewLine, body);
    }

    private static string BuildPublicMessageEmailHtmlBody(
        SellingPortalMessage message,
        string lienCode,
        string? portalUrl)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine("<title>New message on lien offer</title>");
        html.AppendLine("</head>");
        html.AppendLine("<body style=\"margin:0;padding:0;background-color:#f4f5f7;color:#111827;font-family:Arial,Helvetica,sans-serif;\">");
        html.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#f4f5f7\" style=\"width:100%;border-collapse:collapse;background-color:#f4f5f7;\">");
        html.AppendLine("<tr><td align=\"center\" style=\"padding:28px 14px;\">");
        html.AppendLine("<table role=\"presentation\" width=\"560\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" style=\"width:100%;max-width:560px;border-collapse:separate;border-spacing:0;background-color:#ffffff;border-radius:10px;overflow:hidden;\">");
        html.AppendLine("<tr><td bgcolor=\"#071b31\" style=\"background-color:#071b31;padding:28px 30px;\">");
        AppendPublicResponseEmailBrand(html);
        html.AppendLine("<h1 style=\"margin:24px 0 10px 0;color:#ffffff;font-size:24px;line-height:1.25;font-weight:700;letter-spacing:0;\">New message on lien offer</h1>");
        html.Append("<p style=\"margin:0;color:#ffffff;font-size:16px;line-height:1.55;font-weight:400;opacity:.92;\">")
            .Append(Html(message.SenderName))
            .Append(" sent a message regarding lien offer ")
            .Append(Html(lienCode))
            .AppendLine(".</p>");
        html.AppendLine("</td></tr>");
        html.AppendLine("<tr><td bgcolor=\"#ffffff\" style=\"background-color:#ffffff;color:#111827;border:1px solid #e5e5e5;border-top:0;border-radius:0 0 10px 10px;padding:24px 24px 28px;\">");
        html.Append("<p style=\"margin:0 0 20px 0;color:#111827;font-size:15px;line-height:1.6;white-space:pre-wrap;\">")
            .Append(Html(message.Message))
            .AppendLine("</p>");
        if (!string.IsNullOrWhiteSpace(portalUrl))
        {
            html.Append("<a href=\"")
                .Append(Html(portalUrl))
                .AppendLine("\" style=\"display:inline-block;background:#ee7132;color:#ffffff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:700;font-size:14px;line-height:1.2;\">View &amp; Reply</a>");
        }
        html.AppendLine("</td></tr>");
        html.AppendLine("</table>");
        html.AppendLine("</td></tr>");
        html.AppendLine("</table>");
        html.AppendLine("</body></html>");

        return html.ToString();
    }

    private static string BuildPublicMessageNotificationIdempotencyKey(
        SellingPortalMessage message,
        string recipientRole)
    {
        var key = string.Join(":", new[]
        {
            "liens.public-message.email",
            message.TenantId.ToString("N"),
            message.Id.ToString("N"),
            recipientRole.Trim().ToLowerInvariant(),
        });

        return key.Length > 280 ? key[..280] : key;
    }

    private static string? BuildPublicPortalUrl(
        IConfiguration configuration,
        HttpContext httpContext,
        string token)
    {
        var baseUrl = ResolveConfiguredBuyerPortalBaseUrl(configuration);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var host = httpContext.Request.Headers["x-legal-synq-public-host"].FirstOrDefault()
                       ?? httpContext.Request.Host.Value;
            if (string.IsNullOrWhiteSpace(host))
                return null;

            var proto = httpContext.Request.Headers["x-legal-synq-public-proto"].FirstOrDefault()
                        ?? httpContext.Request.Headers["x-forwarded-proto"].FirstOrDefault()
                        ?? (httpContext.Request.IsHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp);
            baseUrl = $"{proto.Replace(":", string.Empty, StringComparison.Ordinal)}://{host}/selling/public";
        }

        return BuildPublicPortalUrl(baseUrl, token);
    }

    private static string? ResolveConfiguredBuyerPortalBaseUrl(IConfiguration configuration)
    {
        var value = configuration["Liens:Selling:BuyerPortalBaseUrl"]?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        var portalHostname = configuration["SYNQLIEN_COMMON_PORTAL_HOSTNAME"]?.Trim();
        if (string.IsNullOrWhiteSpace(portalHostname))
            return null;

        var scheme = portalHostname.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            ? Uri.UriSchemeHttp
            : Uri.UriSchemeHttps;
        var port = portalHostname.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            ? ":5000"
            : string.Empty;
        return $"{scheme}://{portalHostname.TrimEnd('/')}{port}/selling/public";
    }

    private static string BuildPublicPortalUrl(string portalBaseUrl, string token)
    {
        if (portalBaseUrl.Contains("{token}", StringComparison.Ordinal))
            return portalBaseUrl.Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);

        return $"{portalBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(token)}";
    }

    private static string BuildPublicResponseEmailBody(
        string recipientRole,
        string responseVerb,
        string lienCode,
        string buyerName,
        string buyerCompany,
        string sellerCompany,
        string? responseNotes)
    {
        var body = new List<string>
        {
            "LegalSynq",
            $"Lien Offer {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(responseVerb)}",
            string.Empty,
            recipientRole == "buyer"
                ? $"This confirms that your company, {buyerCompany}, {responseVerb} lien offer {lienCode}."
                : $"{buyerName} from {buyerCompany} {responseVerb} lien offer {lienCode}.",
            $"Seller: {sellerCompany}",
        };

        if (!string.IsNullOrWhiteSpace(responseNotes))
            body.Add($"Response notes: {responseNotes.Trim()}");

        return string.Join(Environment.NewLine, body);
    }

    private static string BuildPublicResponseEmailHtmlBody(
        string recipientRole,
        string statusLabel,
        string responseVerb,
        string lienCode,
        string buyerName,
        string buyerCompany,
        string sellerCompany,
        string? responseNotes)
    {
        var title = $"Lien Offer {statusLabel}";
        var isAccepted = string.Equals(statusLabel, "Accepted", StringComparison.Ordinal);
        var badgeBackground = isAccepted ? "#d1fae5" : "#fee2e2";
        var badgeColor = isAccepted ? "#047857" : "#b91c1c";
        var summary = recipientRole == "buyer"
            ? $"This confirms that your company, {buyerCompany}, {responseVerb} lien offer {lienCode}."
            : $"{buyerName} from {buyerCompany} {responseVerb} lien offer {lienCode}.";

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine("<title>").Append(Html(title)).AppendLine("</title>");
        html.AppendLine("</head>");
        html.AppendLine("<body style=\"margin:0;padding:0;background-color:#f4f5f7;color:#111827;font-family:Arial,Helvetica,sans-serif;\">");
        html.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#f4f5f7\" style=\"width:100%;border-collapse:collapse;background-color:#f4f5f7;\">");
        html.AppendLine("<tr><td align=\"center\" style=\"padding:28px 14px;\">");
        html.AppendLine("<table role=\"presentation\" width=\"560\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" style=\"width:100%;max-width:560px;border-collapse:separate;border-spacing:0;background-color:#ffffff;border-radius:10px;overflow:hidden;\">");
        html.AppendLine("<tr><td bgcolor=\"#071b31\" style=\"background-color:#071b31;padding:28px 30px;\">");
        html.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:collapse;margin:0 0 28px 0;\"><tr>");
        html.AppendLine("<td align=\"left\" style=\"vertical-align:middle;padding:0;\">");
        AppendPublicResponseEmailBrand(html);
        html.AppendLine("</td>");
        html.Append("<td align=\"right\" style=\"vertical-align:middle;padding:0;\"><span style=\"display:inline-block;background-color:")
            .Append(badgeBackground)
            .Append(";color:")
            .Append(badgeColor)
            .Append(";border-radius:999px;padding:6px 12px;font-size:12px;font-weight:700;line-height:1.1;white-space:nowrap;\">")
            .Append(Html(statusLabel))
            .AppendLine("</span></td>");
        html.AppendLine("</tr></table>");
        html.Append("<h1 style=\"margin:0 0 10px 0;color:#ffffff;font-size:24px;line-height:1.25;font-weight:700;letter-spacing:0;\">")
            .Append(Html(title))
            .AppendLine("</h1>");
        html.Append("<p style=\"margin:0;color:#ffffff;font-size:16px;line-height:1.55;font-weight:400;opacity:.92;\">")
            .Append(Html(summary))
            .AppendLine("</p>");
        html.AppendLine("</td></tr>");
        html.AppendLine("<tr><td bgcolor=\"#ffffff\" style=\"background-color:#ffffff;color:#111827;border:1px solid #e5e5e5;border-top:0;border-radius:0 0 10px 10px;padding:24px 24px 28px;\">");
        html.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:separate;border-spacing:0;margin:0 0 20px 0;\">");
        AppendPublicResponseEmailRow(html, "Lien Number", lienCode, isFirstRow: true, isLastRow: false);
        AppendPublicResponseEmailRow(html, "Buyer", buyerName, isFirstRow: false, isLastRow: false);
        AppendPublicResponseEmailRow(html, "Funding Company", buyerCompany, isFirstRow: false, isLastRow: false);
        AppendPublicResponseEmailRow(html, "Seller", sellerCompany, isFirstRow: false, isLastRow: string.IsNullOrWhiteSpace(responseNotes));
        if (!string.IsNullOrWhiteSpace(responseNotes))
            AppendPublicResponseEmailRow(html, "Response Notes", responseNotes.Trim(), isFirstRow: false, isLastRow: true);
        html.AppendLine("</table>");
        html.AppendLine("</td></tr>");
        html.AppendLine("</table>");
        html.AppendLine("</td></tr>");
        html.AppendLine("</table>");
        html.AppendLine("</body></html>");

        return html.ToString();
    }

    private static void AppendPublicResponseEmailBrand(StringBuilder html)
    {
        html.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" aria-label=\"LegalSynq\" style=\"border-collapse:collapse;\"><tr><td width=\"36\" style=\"width:36px;padding:0 6px 0 0;vertical-align:middle;\"><img src=\"cid:")
            .Append(LegalSynqBrandIconContentId)
            .AppendLine("\" width=\"36\" height=\"36\" alt=\"\" role=\"presentation\" style=\"display:block;width:36px;height:36px;border:0;outline:none;text-decoration:none;\"></td><td style=\"padding:0;vertical-align:middle;white-space:nowrap;\"><span style=\"color:#ffffff !important;-webkit-text-fill-color:#ffffff;font-size:22px;line-height:1;font-weight:700;letter-spacing:0;\">Legal</span><span style=\"color:#f26a2e !important;-webkit-text-fill-color:#f26a2e;font-size:22px;line-height:1;font-weight:700;letter-spacing:0;\">Synq</span></td></tr></table>");
    }

    private static void AppendPublicResponseEmailRow(
        StringBuilder html,
        string label,
        string value,
        bool isFirstRow,
        bool isLastRow)
    {
        var border = isFirstRow ? "border-top:1px solid #e5e5e5;" : string.Empty;
        var radiusLeft = isFirstRow ? "border-top-left-radius:10px;" : isLastRow ? "border-bottom-left-radius:10px;" : string.Empty;
        var radiusRight = isFirstRow ? "border-top-right-radius:10px;" : isLastRow ? "border-bottom-right-radius:10px;" : string.Empty;

        html.Append("<tr><td style=\"width:42%;padding:14px 14px;color:#6f6f6f;font-size:13px;line-height:1.35;border-left:1px solid #e5e5e5;border-bottom:1px solid #e5e5e5;")
            .Append(border)
            .Append(radiusLeft)
            .Append("\">")
            .Append(Html(label))
            .Append("</td><td align=\"right\" style=\"padding:14px 14px;color:#111111;font-size:15px;line-height:1.35;font-weight:600;border-right:1px solid #e5e5e5;border-bottom:1px solid #e5e5e5;")
            .Append(border)
            .Append(radiusRight)
            .Append("\">")
            .Append(Html(value))
            .AppendLine("</td></tr>");
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
        var messages = await ResolveMessagesAsync(db, accessLink, ct);
        var buyerResponseAccessLink = await ResolveBuyerResponseAccessLinkAsync(db, accessLink, ct);

        return new PublicPortalView(
            accessLink,
            lien,
            caseEntity,
            buyerContact,
            sellerContact,
            handlingLawFirm,
            caseManager,
            documents,
            messages,
            buyerResponseAccessLink);
    }

    private static async Task<IReadOnlyList<SellingPortalMessage>> ResolveMessagesAsync(
        LiensDbContext db,
        SellingBuyerAccessLink accessLink,
        CancellationToken ct)
        => await db.SellingPortalMessages
            .AsNoTracking()
            .Where(message =>
                message.TenantId == accessLink.TenantId &&
                message.LienId == accessLink.LienId &&
                message.SellerOrgId == accessLink.SellerOrgId &&
                message.BuyerOrgId == accessLink.BuyerOrgId &&
                message.BuyerContactId == accessLink.BuyerContactId)
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.Id)
            .ToListAsync(ct);

    private static Task<SellingBuyerAccessLink?> ResolveBuyerResponseAccessLinkAsync(
        LiensDbContext db,
        SellingBuyerAccessLink accessLink,
        CancellationToken ct)
    {
        if (!string.Equals(accessLink.Purpose, SellingAccessLinkPurposes.ConfirmSaleSellerView, StringComparison.Ordinal))
            return Task.FromResult<SellingBuyerAccessLink?>(null);

        return db.SellingBuyerAccessLinks
            .AsNoTracking()
            .Where(link =>
                link.TenantId == accessLink.TenantId &&
                link.LienId == accessLink.LienId &&
                link.SellerOrgId == accessLink.SellerOrgId &&
                link.BuyerOrgId == accessLink.BuyerOrgId &&
                link.BuyerContactId == accessLink.BuyerContactId &&
                link.Purpose == SellingAccessLinkPurposes.ConfirmSaleBuyerResponse)
            .OrderByDescending(link => link.RespondedAtUtc.HasValue)
            .ThenByDescending(link => link.RespondedAtUtc)
            .ThenByDescending(link => link.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
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
    {
        var responseAccessLink = view.BuyerResponseAccessLink ?? view.AccessLink;

        return new(
            ResolvePublicAudience(view.AccessLink),
            new PublicBuyerAccessLinkResponse(
                view.AccessLink.CreatedAtUtc,
                view.AccessLink.ExpiresAtUtc,
                view.AccessLink.LastAccessedAtUtc,
                view.AccessLink.NotificationSubmittedAtUtc,
                responseAccessLink.ResponseStatus,
                responseAccessLink.ResponseAmount,
                responseAccessLink.ResponseNotes,
                responseAccessLink.RespondedAtUtc),
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
                view.BuyerContact?.Email,
                view.BuyerContact?.Phone),
            new PublicBuyerCaseResponse(
                view.HandlingLawFirm,
                view.CaseManager),
            view.Documents
                .Select(document => new PublicBuyerDocumentResponse(
                    document.FileName,
                    document.Category,
                    document.SizeOrType))
                .ToList(),
            view.Messages
                .Select(MapPublicMessage)
                .ToList());
    }

    private static PublicPortalMessageResponse MapPublicMessage(SellingPortalMessage message)
        => new(
            message.Id,
            message.SenderType,
            message.SenderName,
            message.SenderEmail,
            message.Message,
            message.CreatedAtUtc);

    private static bool IsSupportedPublicPurpose(string purpose)
        => string.Equals(purpose, SellingAccessLinkPurposes.ConfirmSaleBuyerResponse, StringComparison.Ordinal) ||
           string.Equals(purpose, SellingAccessLinkPurposes.ConfirmSaleSellerView, StringComparison.Ordinal);

    private static bool IsBuyerResponseLink(SellingBuyerAccessLink accessLink)
        => string.Equals(accessLink.Purpose, SellingAccessLinkPurposes.ConfirmSaleBuyerResponse, StringComparison.Ordinal);

    private static string ResolvePublicAudience(SellingBuyerAccessLink accessLink)
        => string.Equals(accessLink.Purpose, SellingAccessLinkPurposes.ConfirmSaleSellerView, StringComparison.Ordinal)
            ? "seller"
            : "buyer";

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

    private static string Html(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

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

    private static bool IsNotificationSubmittedStatus(string? status)
        => string.Equals(status, "sent", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "accepted", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase);

    private static string BuildPublicResponseNotificationIdempotencyKey(
        SellingBuyerAccessLink accessLink,
        string responseStatus,
        string recipientRole)
    {
        var key = string.Join(":", new[]
        {
            "liens.public-response.email",
            accessLink.TenantId.ToString("N"),
            accessLink.Id.ToString("N"),
            responseStatus.Trim().ToLowerInvariant(),
            recipientRole.Trim().ToLowerInvariant(),
        });

        return key.Length > 280 ? key[..280] : key;
    }

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
        IReadOnlyList<PublicDocumentView> Documents,
        IReadOnlyList<SellingPortalMessage> Messages,
        SellingBuyerAccessLink? BuyerResponseAccessLink);

    private sealed record PublicDocumentView(string FileName, string? Category, string SizeOrType);

    private sealed record PublicBuyerPortalResponse(
        string Audience,
        PublicBuyerAccessLinkResponse AccessLink,
        PublicBuyerLienResponse Lien,
        PublicBuyerSellerResponse Seller,
        PublicBuyerOrganizationResponse Buyer,
        PublicBuyerCaseResponse Case,
        IReadOnlyList<PublicBuyerDocumentResponse> Documents,
        IReadOnlyList<PublicPortalMessageResponse> Messages);

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
        string? Email,
        string? Phone);

    private sealed record PublicBuyerCaseResponse(
        string? HandlingLawFirm,
        string? CaseManager);

    private sealed record PublicBuyerDocumentResponse(
        string FileName,
        string? Category,
        string SizeOrType);

    private sealed record PublicPortalMessageResponse(
        Guid Id,
        string SenderType,
        string SenderName,
        string? SenderEmail,
        string Message,
        DateTime CreatedAtUtc);

    private sealed record PublicBuyerPortalErrorResponse(PublicBuyerPortalError Error);

    private sealed record PublicBuyerPortalError(string Code, string Title, string Message);

    private sealed record PublicPortalMessageRequest(string? Message);

    private sealed record PublicBuyerAcceptLienRequest(string? Notes, string? Message);

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
