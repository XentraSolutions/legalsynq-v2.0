using System.Security.Claims;
using BuildingBlocks.Authorization;

namespace Xenia.Api.Endpoints;

internal static class XeniaEndpointContext
{
    internal static Guid ResolveTenantId(HttpContext context, Guid? requestTenantId = null)
    {
        if (requestTenantId.HasValue && requestTenantId.Value != Guid.Empty)
            return requestTenantId.Value;

        var candidates = new[]
        {
            context.User.FindFirstValue("tenant_id"),
            context.User.FindFirstValue("tenantId"),
            context.Request.Headers["X-Tenant-Id"].FirstOrDefault(),
        };

        foreach (var candidate in candidates)
        {
            if (Guid.TryParse(candidate, out var tenantId) && tenantId != Guid.Empty)
                return tenantId;
        }

        throw new InvalidOperationException("A tenant identifier is required for Xenia requests.");
    }

    internal static string ResolveActorUserId(HttpContext context) =>
        context.User.FindFirstValue("sub")
        ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? "service:xenia";

    internal static string ResolveCallingProductCode(HttpContext context, string? requestProductCode = null)
    {
        var candidate = requestProductCode
            ?? context.Request.Headers["X-Product-Code"].FirstOrDefault()
            ?? context.User.FindFirstValue("product_code");

        return string.IsNullOrWhiteSpace(candidate)
            ? ProductCodes.Xenia
            : candidate.Trim().ToUpperInvariant();
    }

    internal static void RequireXeniaAccess(HttpContext context)
    {
        if (context.User.IsInRole("PlatformAdmin"))
            return;

        var productCodes = context.User.FindAll("product_codes")
            .Select(claim => claim.Value?.Trim()?.ToUpperInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!productCodes.Contains(ProductCodes.Xenia))
            throw new InvalidOperationException("The current user does not have Xenia product access.");
    }

    internal static string ToServerSentEvents(IReadOnlyList<string> chunks, Guid messageId)
    {
        var lines = new List<string>();
        foreach (var chunk in chunks)
        {
            lines.Add("event: message.delta");
            lines.Add($"data: {System.Text.Json.JsonSerializer.Serialize(new { messageId, delta = chunk })}");
            lines.Add(string.Empty);
        }

        lines.Add("event: message.completed");
        lines.Add($"data: {System.Text.Json.JsonSerializer.Serialize(new { messageId, done = true })}");
        lines.Add(string.Empty);

        return string.Join('\n', lines);
    }
}
