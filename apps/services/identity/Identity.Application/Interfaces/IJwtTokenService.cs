using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(
        User user,
        Tenant tenant,
        IEnumerable<string> roles,
        Organization? organization = null,
        IEnumerable<string>? productRoles = null,
        int? sessionTimeoutMinutes = null,
        IEnumerable<string>? productCodes = null,
        IEnumerable<string>? permissions = null,
        IEnumerable<Guid>? tenantIds = null);

    /// <summary>
    /// BE-BIO-008/SEC-008: mints a short-lived access token from freshly resolved
    /// authorization context and binds it to the device session.
    /// </summary>
    (string Token, DateTime ExpiresAtUtc) GenerateRefreshedAccessToken(
        User user,
        Tenant tenant,
        Guid deviceSessionId);

    (string Token, DateTime ExpiresAtUtc) GenerateRefreshedAccessToken(
        User user,
        Tenant tenant,
        Guid deviceSessionId,
        IEnumerable<string> roles,
        Organization? organization,
        IEnumerable<string> productRoles,
        int sessionTimeoutMinutes,
        IEnumerable<string> productCodes,
        IEnumerable<string> permissions,
        IEnumerable<Guid> tenantIds) => GenerateRefreshedAccessToken(user, tenant, deviceSessionId);
}
