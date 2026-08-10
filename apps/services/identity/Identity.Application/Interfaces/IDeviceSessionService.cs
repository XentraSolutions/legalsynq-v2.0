using Identity.Application.DTOs;

namespace Identity.Application.Interfaces;

/// <summary>
/// BE-BIO: device-specific refresh-token lifecycle — issuance, atomic rotation
/// with reuse detection, biometric-enable administrative flag, and revocation
/// (single session, all sessions, or by token family).
/// </summary>
public interface IDeviceSessionService
{
    /// <summary>BE-BIO-001/010: called from AuthService.LoginAsync when the request opts in via DeviceInfo.</summary>
    Task<RefreshTokenResponse> CreateDeviceSessionAsync(
        Guid userId,
        Guid tenantId,
        DeviceInfo deviceInfo,
        string accessToken,
        DateTime accessTokenExpiresAtUtc,
        CancellationToken ct = default);

    /// <summary>BE-BIO-004/006/007: the atomic rotation transaction. See DeviceSessionService.RefreshAsync for the full algorithm.</summary>
    Task<DeviceSessionRefreshResult> RefreshAsync(
        string rawRefreshToken,
        Guid deviceSessionId,
        string? ipAddress,
        CancellationToken ct = default);

    /// <summary>BE-BIO-011: administrative flag only — never itself a source of authorization (SEC-006).</summary>
    Task<bool> EnableBiometricAsync(Guid userId, Guid deviceSessionId, CancellationToken ct = default);

    /// <summary>BE-BIO-012: disables the flag and revokes the session/token in the same operation.</summary>
    Task<bool> DisableBiometricAsync(Guid userId, Guid deviceSessionId, CancellationToken ct = default);

    /// <summary>BE-BIO-013: idempotent — returns true whether or not the session was already revoked.</summary>
    Task<bool> LogoutCurrentAsync(Guid userId, Guid deviceSessionId, CancellationToken ct = default);

    /// <summary>BE-BIO-014: user-initiated logout-all. Caller must have already enforced step-up (recent primary auth).</summary>
    Task<int> LogoutAllAsync(Guid userId, CancellationToken ct = default);

    /// <summary>BE-BIO-017: internal-use revoke-all invoked by privileged flows (password reset, account lock, compromise) — no step-up check, always carries a specific reason.</summary>
    Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default);

    /// <summary>BE-BIO-015: no token material in the projection.</summary>
    Task<IReadOnlyList<DeviceSessionSummary>> ListSessionsAsync(Guid userId, Guid? currentDeviceSessionId, CancellationToken ct = default);

    /// <summary>BE-BIO-016: IDOR-checked — returns false (not found/not owned) rather than throwing, so the endpoint can map to 404.</summary>
    Task<bool> RevokeSessionAsync(Guid userId, Guid deviceSessionId, string reason, CancellationToken ct = default);

    /// <summary>BE-BIO-007: reuse-detection cascade — revokes every session/ledger entry in the family and marks the session Compromised.</summary>
    Task RevokeByTokenFamilyAsync(Guid tokenFamilyId, string reason, CancellationToken ct = default);

    /// <summary>SEC-014: IDOR-checked lookup used by the endpoint layer's step-up ("recent primary auth") gate on logout-all and cross-device revocation. Null if not found/not owned.</summary>
    Task<DateTime?> GetLastSuccessfulAuthAsync(Guid userId, Guid deviceSessionId, CancellationToken ct = default);
}

/// <summary>Discriminated result for RefreshAsync — exactly one of Success/ErrorCode is set.</summary>
public sealed class DeviceSessionRefreshResult
{
    public bool IsSuccess { get; private init; }
    public RefreshTokenResponse? Response { get; private init; }
    public string? ErrorCode { get; private init; }

    public static DeviceSessionRefreshResult Success(RefreshTokenResponse response) =>
        new() { IsSuccess = true, Response = response };

    public static DeviceSessionRefreshResult Failure(string errorCode) =>
        new() { IsSuccess = false, ErrorCode = errorCode };
}
