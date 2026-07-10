namespace Xenia.Application.Email;

/// <summary>
/// Platform-neutral contract for resolving secret references.
///
/// Xenia never stores plaintext credentials. Email source authentication
/// material is represented by an opaque reference ID. This service resolves
/// that reference to a usable (but still protected) value at runtime.
///
/// Implementations:
/// - Development stub: <c>UnavailableSecretReferenceService</c> — reports unavailable honestly.
/// - Production: wired to the platform's secret provider (vault, AWS Secrets Manager, etc.).
///
/// Replace the implementation without changing EmailSource or any caller.
/// </summary>
public interface ISecretReferenceService
{
    /// <summary>Whether the secret backend is configured in this environment.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Validates that a reference ID is syntactically well-formed.
    /// Does NOT check whether the secret exists.
    /// </summary>
    bool IsValidReferenceFormat(string referenceId);

    /// <summary>
    /// Attempts to resolve the secret value for the given reference.
    /// Returns null if the reference does not exist or the service is unavailable.
    /// NEVER logs the returned value.
    /// </summary>
    Task<SecretResolutionResult> ResolveAsync(string referenceId, CancellationToken ct = default);
}

/// <summary>Result of a secret resolution attempt.</summary>
public sealed record SecretResolutionResult
{
    public required bool Success { get; init; }
    public required string? ErrorCode { get; init; }
    public required string? ErrorSummary { get; init; }

    /// <summary>
    /// Resolved secret value. Present only when Success=true.
    /// MUST NOT be logged, serialized to disk, or included in API responses.
    /// </summary>
    public string? Value { get; init; }

    public static SecretResolutionResult Unavailable() => new()
    {
        Success = false,
        ErrorCode = "SECRET_SERVICE_UNAVAILABLE",
        ErrorSummary = "The secret reference service is not configured in this environment.",
    };

    public static SecretResolutionResult NotFound(string referenceId) => new()
    {
        Success = false,
        ErrorCode = "SECRET_NOT_FOUND",
        ErrorSummary = $"Secret reference '{referenceId}' was not found.",
    };

    public static SecretResolutionResult Ok(string value) => new()
    {
        Success = true,
        ErrorCode = null,
        ErrorSummary = null,
        Value = value,
    };
}
