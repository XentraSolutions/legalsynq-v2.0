namespace Identity.Application.DTOs;

/// <summary>
/// Response from GET /api/auth/me.
/// Returned by the Identity service after validating the caller's JWT.
/// The Next.js BFF forwards this to the client — the raw token is never sent.
/// </summary>
public record AuthMeResponse(
    string UserId,
    string Email,
    string TenantId,
    string TenantCode,
    string? OrgId,
    string? OrgType,
    string? OrgName,
    List<string> ProductRoles,
    List<string> SystemRoles,
    DateTime ExpiresAtUtc,
    int SessionTimeoutMinutes = 30,
    Guid? AvatarDocumentId = null);
