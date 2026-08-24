using Documents.Domain.ValueObjects;
using System.Security.Claims;

namespace Documents.Infrastructure.Auth;

public static class JwtPrincipalExtractor
{
    /// <summary>
    /// Extracts a <see cref="Principal"/> from ASP.NET Core's ClaimsPrincipal.
    /// Supports both standard and custom claim naming conventions used by the platform.
    /// </summary>
    public static Principal Extract(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub")
               ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("userId")
               ?? throw new UnauthorizedAccessException("JWT missing 'sub' claim");

        var tenantIdRaw = user.FindFirstValue("tenantId")
                       ?? user.FindFirstValue("tenant_id")
                       ?? throw new UnauthorizedAccessException("JWT missing 'tenantId' claim");

        var userId = ResolveUserId(user, sub);

        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
            throw new UnauthorizedAccessException("JWT 'tenantId' claim is not a valid UUID");

        var email = user.FindFirstValue("email")
                 ?? user.FindFirstValue(ClaimTypes.Email);

        // Roles may be a single claim or multiple claims
        var roles = user.FindAll("roles")
            .Concat(user.FindAll("role"))
            .Concat(user.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        var productRoles = user.FindAll("product_roles")
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new Principal
        {
            UserId   = userId,
            TenantId = tenantId,
            Email    = email,
            Roles    = roles,
            ProductRoles = productRoles,
        };
    }

    private static Guid ResolveUserId(ClaimsPrincipal user, string sub)
    {
        if (Guid.TryParse(sub, out var userId))
            return userId;

        var actor = user.FindFirstValue("actor");
        if (actor is not null &&
            actor.StartsWith("user:", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(actor.AsSpan(5), out var actorUserId))
        {
            return actorUserId;
        }

        return Guid.Empty;
    }
}
