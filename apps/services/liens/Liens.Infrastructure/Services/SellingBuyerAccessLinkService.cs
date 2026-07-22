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
    private const string ConfirmSalePurpose = "ConfirmSale";

    private readonly LiensDbContext _db;
    private readonly IConfiguration _configuration;

    public SellingBuyerAccessLinkService(
        LiensDbContext db,
        IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<SellingBuyerAccessLinkResult> CreateOrGetForConfirmSaleAsync(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid buyerOrgId,
        Guid buyerContactId,
        Guid actingUserId,
        string idempotencyKey,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        var portalBaseUrl = ResolveBuyerPortalBaseUrl();
        var trimmedIdempotencyKey = idempotencyKey.Trim();

        var existing = await _db.SellingBuyerAccessLinks
            .Where(l => l.TenantId == tenantId && l.IdempotencyKey == trimmedIdempotencyKey)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            return Map(existing, portalBaseUrl, alreadyExisted: true);
        }

        var token = GenerateToken();
        var link = SellingBuyerAccessLink.Create(
            tenantId,
            lienId,
            sellerOrgId,
            buyerOrgId,
            buyerContactId,
            token,
            ConfirmSalePurpose,
            trimmedIdempotencyKey,
            DateTime.UtcNow.Add(ttl),
            actingUserId);

        await _db.SellingBuyerAccessLinks.AddAsync(link, ct);
        await _db.SaveChangesAsync(ct);

        return Map(link, portalBaseUrl, alreadyExisted: false);
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
        var value = _configuration["Liens:Selling:BuyerPortalBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Replace("{token}", "token-preview", StringComparison.Ordinal), UriKind.Absolute, out _))
        {
            throw new ValidationException("Buyer portal base URL is required.",
                new Dictionary<string, string[]>
                {
                    ["buyerPortalBaseUrl"] = ["Configure Liens:Selling:BuyerPortalBaseUrl with an absolute buyer portal URL."],
                });
        }

        return value;
    }

    private static SellingBuyerAccessLinkResult Map(
        SellingBuyerAccessLink link,
        string portalBaseUrl,
        bool alreadyExisted)
        => new(
            link.Id,
            link.Token,
            BuildPortalUrl(portalBaseUrl, link.Token),
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
