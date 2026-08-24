namespace Identity.Application.DTOs;

/// <summary>
/// BE-BIO: Client-supplied device metadata, sent either as part of login
/// (to opt into device-session/refresh-token issuance) or standalone on the
/// refresh/logout endpoints. Never carries biometric data — biometric capability
/// is a device-local concept the backend never receives or validates (SEC-001/SEC-006).
/// </summary>
public record DeviceInfo(
    string Platform,
    string? AppVersion = null,
    string? OsVersion = null,
    string? DeviceDisplayName = null);

/// <summary>BE-BIO-015: device-session list projection. Never includes token material.</summary>
public record DeviceSessionSummary(
    Guid Id,
    string DeviceDisplayName,
    string Platform,
    DateTime LastUsedAtUtc,
    DateTime CreatedAtUtc,
    bool IsCurrentDevice,
    bool BiometricEnabled);

/// <summary>BE-BIO-004/006: response for both initial device-session creation (via login) and refresh rotation.</summary>
public record RefreshTokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid DeviceSessionId);
