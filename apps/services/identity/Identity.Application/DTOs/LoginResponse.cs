namespace Identity.Application.DTOs;

public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserResponse User,
    IReadOnlyList<TenantSummary>? Tenants = null,
    // BE-BIO: populated only when the request included DeviceInfo. Null for every
    // existing caller — additive/nullable so current JSON consumers are unaffected.
    string? RefreshToken = null,
    DateTime? RefreshTokenExpiresAtUtc = null,
    Guid? DeviceSessionId = null);
