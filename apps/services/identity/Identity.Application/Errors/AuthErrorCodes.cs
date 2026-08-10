namespace Identity.Application.Errors;

/// <summary>
/// BE-BIO-018: machine-readable error codes for the device-session/refresh-token
/// endpoints, surfaced via Results.Problem(...) `errorCode` extension so mobile
/// clients can branch without string-matching the human-readable message.
///
/// SEC-010: REFRESH_TOKEN_REUSED is intentionally not a distinct externally-visible
/// code — a confirmed reuse (token theft) response uses REFRESH_TOKEN_INVALID
/// externally so an attacker cannot tell their theft attempt was specifically
/// detected. The true classification is retained only in the DB Status column
/// and the audit trail.
/// </summary>
public static class AuthErrorCodes
{
    public const string RefreshTokenInvalid = "REFRESH_TOKEN_INVALID";
    public const string RefreshTokenExpired = "REFRESH_TOKEN_EXPIRED";
    public const string RefreshTokenRevoked = "REFRESH_TOKEN_REVOKED";
    public const string DeviceSessionRevoked = "DEVICE_SESSION_REVOKED";
    public const string DeviceSessionNotFound = "DEVICE_SESSION_NOT_FOUND";
    public const string AccountDisabled = "ACCOUNT_DISABLED";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string SessionReauthenticationRequired = "SESSION_REAUTHENTICATION_REQUIRED";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
}
