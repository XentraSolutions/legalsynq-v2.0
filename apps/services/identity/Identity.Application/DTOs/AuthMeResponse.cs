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
    Guid? AvatarDocumentId = null,
    /// <summary>
    /// Frontend-friendly product codes (e.g. "SynqFund", "CareConnect") for every product
    /// that is currently enabled at the tenant level.  Used by the tenant portal to show
    /// only the products the tenant has licensed.  Derived from TenantProduct.IsEnabled.
    /// </summary>
    List<string>? EnabledProducts = null,
    /// <summary>
    /// Primary phone number on file for the user, in E.164 form. Surfaced so
    /// the profile page can display the current value without a second round-trip.
    /// Null when the user has not provided a phone yet.
    /// </summary>
    string? Phone = null);
