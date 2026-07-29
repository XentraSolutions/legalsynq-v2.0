using System.Security.Cryptography;
using BuildingBlocks.Exceptions;
using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Liens.Infrastructure.Services;

public sealed class SellingBuyerAccessLinkService : ISellingBuyerAccessLinkService
{
    private const string LegacyConfirmSaleRoute = "/api/liens/selling/liens/{lienId}/confirm-sale";
    private const string ConfirmSaleSellerViewRoute = "/api/liens/selling/liens/{lienId}/confirm-sale/seller-view";

    private readonly LiensDbContext _db;
    private readonly IConfiguration _configuration;

    public SellingBuyerAccessLinkService(
        LiensDbContext db,
        IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public Task<SellingBuyerAccessLinkResult> CreateOrGetForConfirmSaleAsync(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid buyerOrgId,
        Guid buyerContactId,
        Guid actingUserId,
        string idempotencyKey,
        TimeSpan ttl,
        CancellationToken ct = default)
        => CreateOrGetAsync(
            tenantId,
            lienId,
            sellerOrgId,
            buyerOrgId,
            buyerContactId,
            actingUserId,
            LegacyConfirmSaleRoute,
            SellingAccessLinkPurposes.ConfirmSaleBuyerResponse,
            idempotencyKey,
            ttl,
            ct);

    public Task<SellingBuyerAccessLinkResult> CreateAsync(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid buyerOrgId,
        Guid buyerContactId,
        Guid actingUserId,
        string route,
        string idempotencyKey,
        TimeSpan ttl,
        CancellationToken ct = default)
        => CreateOrGetAsync(
            tenantId,
            lienId,
            sellerOrgId,
            buyerOrgId,
            buyerContactId,
            actingUserId,
            route,
            SellingAccessLinkPurposes.ConfirmSaleBuyerResponse,
            idempotencyKey,
            ttl,
            ct);

    public Task<SellingBuyerAccessLinkResult> CreateOrGetForConfirmSaleSellerViewAsync(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid buyerOrgId,
        Guid buyerContactId,
        Guid actingUserId,
        string idempotencyKey,
        TimeSpan ttl,
        CancellationToken ct = default)
        => CreateOrGetAsync(
            tenantId,
            lienId,
            sellerOrgId,
            buyerOrgId,
            buyerContactId,
            actingUserId,
            ConfirmSaleSellerViewRoute,
            SellingAccessLinkPurposes.ConfirmSaleSellerView,
            idempotencyKey,
            ttl,
            ct);

    private async Task<SellingBuyerAccessLinkResult> CreateOrGetAsync(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid buyerOrgId,
        Guid buyerContactId,
        Guid actingUserId,
        string route,
        string purpose,
        string idempotencyKey,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "Access-link TTL must be positive.");

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var portalBaseUrl = ResolveBuyerPortalBaseUrl();
        var trimmedRoute = route?.Trim();
        var trimmedIdempotencyKey = idempotencyKey.Trim();

        ArgumentException.ThrowIfNullOrWhiteSpace(trimmedRoute, nameof(route));

        var existing = await _db.SellingBuyerAccessLinks
            .Where(l =>
                l.TenantId == tenantId &&
                l.SellerOrgId == sellerOrgId &&
                l.LienId == lienId &&
                l.BuyerOrgId == buyerOrgId &&
                l.BuyerContactId == buyerContactId &&
                l.CreatedByUserId == actingUserId &&
                l.Route == trimmedRoute &&
                l.IdempotencyKey == trimmedIdempotencyKey)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            // A raw token is intentionally unrecoverable after the first response.
            // Replays may expose link metadata, but never recreate the secret URL.
            return Map(existing, portalBaseUrl, rawToken: null, alreadyExisted: true);
        }

        var token = GenerateToken();
        var link = SellingBuyerAccessLink.Create(
            tenantId,
            lienId,
            sellerOrgId,
            buyerOrgId,
            buyerContactId,
            token,
            purpose,
            trimmedRoute,
            trimmedIdempotencyKey,
            DateTime.UtcNow.Add(ttl),
            actingUserId);

        await _db.SellingBuyerAccessLinks.AddAsync(link, ct);
        await _db.SaveChangesAsync(ct);

        return Map(link, portalBaseUrl, token, alreadyExisted: false);
    }

    public async Task MarkNotificationSubmittedAsync(
        Guid tenantId,
        Guid accessLinkId,
        Guid? notificationId,
        string notificationStatus,
        CancellationToken ct = default)
    {
        var link = await _db.SellingBuyerAccessLinks
            .Where(l => l.TenantId == tenantId && l.Id == accessLinkId)
            .FirstOrDefaultAsync(ct);

        if (link is null)
            return;

        link.MarkNotificationSubmitted(notificationId, notificationStatus);
        await _db.SaveChangesAsync(ct);
    }

    private string ResolveBuyerPortalBaseUrl()
    {
        var value = ResolveConfiguredBuyerPortalBaseUrl();
        var previewValue = value?.Replace("{token}", "token-preview", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(previewValue, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ValidationException("Buyer portal base URL is required.",
                new Dictionary<string, string[]>
                {
                    ["buyerPortalBaseUrl"] = ["Configure Liens:Selling:BuyerPortalBaseUrl with an absolute buyer portal URL, or configure SYNQLIEN_COMMON_PORTAL_HOSTNAME so the API can derive the local buyer portal URL."],
                });
        }

        if (uri.IsLoopback && !IsNamedLocalhostAlias(uri.Host))
        {
            throw new ValidationException("Buyer portal base URL must be externally reachable.",
                new Dictionary<string, string[]>
                {
                    ["buyerPortalBaseUrl"] = ["Configure Liens:Selling:BuyerPortalBaseUrl with an externally reachable URL or a named .localhost demo alias; literal localhost and 127.0.0.1 links do not work from outbound email."],
                });
        }

        return value;
    }

    private string? ResolveConfiguredBuyerPortalBaseUrl()
    {
        var value = _configuration["Liens:Selling:BuyerPortalBaseUrl"]?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        var portalHostname = _configuration["SYNQLIEN_COMMON_PORTAL_HOSTNAME"]?.Trim();
        if (string.IsNullOrWhiteSpace(portalHostname))
            return null;

        var scheme = IsNamedLocalhostAlias(portalHostname) ? Uri.UriSchemeHttp : Uri.UriSchemeHttps;
        var port = IsNamedLocalhostAlias(portalHostname) ? ":5000" : string.Empty;
        return $"{scheme}://{portalHostname.TrimEnd('/')}{port}/selling/public";
    }

    private static bool IsNamedLocalhostAlias(string host)
        => host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) &&
           !host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private static SellingBuyerAccessLinkResult Map(
        SellingBuyerAccessLink link,
        string portalBaseUrl,
        string? rawToken,
        bool alreadyExisted)
        => new(
            link.Id,
            rawToken,
            rawToken is null ? string.Empty : BuildPortalUrl(portalBaseUrl, rawToken),
            link.ExpiresAtUtc,
            alreadyExisted,
            link.NotificationId,
            link.NotificationStatus,
            link.NotificationSubmittedAtUtc);

    private static string BuildPortalUrl(string portalBaseUrl, string token)
    {
        if (portalBaseUrl.Contains("{token}", StringComparison.Ordinal))
            return portalBaseUrl.Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);

        return $"{portalBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(token)}";
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
