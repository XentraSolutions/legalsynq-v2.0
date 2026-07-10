using Microsoft.Extensions.Logging;
using Xenia.Application.Email;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Development stub for <see cref="ISecretReferenceService"/>.
///
/// Used when no real secret backend is configured. Reports honestly that the
/// secret service is unavailable — never claims to resolve secrets.
///
/// Replace with a real implementation (AWS Secrets Manager, platform vault, etc.)
/// by registering a different ISecretReferenceService in DependencyInjection.cs.
/// This stub will be skipped automatically when a real implementation is registered.
/// </summary>
internal sealed class UnavailableSecretReferenceService : ISecretReferenceService
{
    private readonly ILogger<UnavailableSecretReferenceService> _logger;

    public UnavailableSecretReferenceService(ILogger<UnavailableSecretReferenceService> logger)
        => _logger = logger;

    public bool IsConfigured => false;

    public bool IsValidReferenceFormat(string referenceId)
    {
        if (string.IsNullOrWhiteSpace(referenceId)) return false;
        if (referenceId.Length > 500) return false;
        return true;
    }

    public Task<SecretResolutionResult> ResolveAsync(string referenceId, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "[SECRET-FALLBACK] Secret reference service is not configured. " +
            "Cannot resolve reference. ReferenceId prefix='{Prefix}'",
            referenceId.Length > 8 ? referenceId[..8] + "..." : "***");

        return Task.FromResult(SecretResolutionResult.Unavailable());
    }
}
