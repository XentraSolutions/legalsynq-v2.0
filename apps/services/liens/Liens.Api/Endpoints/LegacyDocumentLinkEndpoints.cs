using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Domain;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Api.Endpoints;

/// <summary>
/// Resolves legacy SL-CORE object keys retained in servicing-item metadata.
/// This exists solely to preserve the former browser Documents-service request
/// shape for records that were migrated as URL links rather than file bytes.
/// </summary>
public static class LegacyDocumentLinkEndpoints
{
    private const string LegacyDocumentHost = "legal-dmm-prod.legalsynq.com";

    public static void MapLegacyDocumentLinkEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/legacy-document-links")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/{objectKey}/resolve", Resolve)
            .RequirePermission(LiensPermissions.CaseRead);
    }

    private static async Task<IResult> Resolve(
        string objectKey,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        if (!IsSafeObjectKey(objectKey))
            return Results.BadRequest(new { error = new { code = "invalid_legacy_document_key" } });

        var notes = await db.ServicingItems.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && (item.TaskType == "LegacyCaseDocument"
                    || item.TaskType == "LegacyLienDocument"
                    || item.TaskType == "LegacyMedicalDocument")
                && item.Notes != null
                && item.Notes.Contains($"/{objectKey}"))
            .Select(item => item.Notes!)
            .ToListAsync(ct);

        var urls = notes
            .Select(ExtractLegacyDocumentUrl)
            .Where(url => IsMatchingLegacyUrl(url, objectKey))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return urls.Count switch
        {
            0 => Results.NotFound(new { error = new { code = "legacy_document_not_found" } }),
            1 => Results.Ok(new { url = urls[0] }),
            _ => Results.Conflict(new { error = new { code = "ambiguous_legacy_document_key" } }),
        };
    }

    private static bool IsSafeObjectKey(string value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Length <= 255
           && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');

    private static string? ExtractLegacyDocumentUrl(string notes)
    {
        foreach (var segment in notes.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = segment[..separator].Trim();
            if (!string.Equals(key, "url", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "documentUrl", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = segment[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static bool IsMatchingLegacyUrl(string? value, string objectKey)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var url)
            && string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && string.Equals(url.Host, LegacyDocumentHost, StringComparison.OrdinalIgnoreCase)
            && url.IsDefaultPort
            && string.Equals(Path.GetFileName(url.AbsolutePath), objectKey, StringComparison.Ordinal);
    }
}
