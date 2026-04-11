using System.Security.Claims;
using Identity.Application.DTOs;

namespace Identity.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default);

    /// <summary>
    /// Builds an AuthMeResponse from a validated ClaimsPrincipal.
    /// Called by GET /api/auth/me after JWT validation — no DB lookup required
    /// for the basic session fields since all data is encoded in the token.
    /// </summary>
    Task<AuthMeResponse> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken ct = default);

    /// <summary>
    /// Accepts a pending invitation, sets the new password, and activates the account.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown for all validation failures (invalid token, already accepted, expired).</exception>
    Task AcceptInviteAsync(string token, string newPassword, CancellationToken ct = default);

    /// <summary>
    /// Verifies the caller's current password and replaces it with a new one.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if user not found or current password is incorrect.</exception>
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Validates a password-reset token, sets the new password, and invalidates existing sessions.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown for all validation failures (invalid token, already used, expired).</exception>
    Task ConfirmPasswordResetAsync(string token, string newPassword, CancellationToken ct = default);

    /// <summary>
    /// Requests a self-service password reset. Returns the raw token when the user was found,
    /// or null when the tenant/user could not be located (caller always returns a generic message).
    /// </summary>
    Task<string?> ForgotPasswordAsync(string tenantCode, string email, CancellationToken ct = default);
}
