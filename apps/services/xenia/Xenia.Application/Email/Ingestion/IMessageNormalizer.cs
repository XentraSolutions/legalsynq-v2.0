using Xenia.Domain.Email;

namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Normalizes a provider-specific message envelope into the canonical message model.
///
/// Responsibilities:
/// - Sanitize headers (remove sensitive entries; cap size)
/// - Normalize email addresses (lower-case, trim)
/// - Normalize timestamps to UTC
/// - Truncate body fields to configured size limits
/// - Generate plain-text preview from HTML or plain body
/// - Compute content hash
/// - Validate required fields
/// - Remove active content (scripts, external resources) from HTML
/// </summary>
public interface IMessageNormalizer
{
    /// <summary>
    /// Normalizes a provider envelope. Must not throw; validation errors produce a
    /// result with <c>IsValid = false</c>.
    /// </summary>
    NormalizationResult Normalize(
        ProviderMessageEnvelope envelope,
        EmailProviderType providerType,
        string? correlationId = null);
}

/// <summary>Result of normalizing a single provider envelope.</summary>
public sealed record NormalizationResult
{
    public required bool IsValid { get; init; }
    public NormalizedMessage? Message { get; init; }
    public string? ErrorCode { get; init; }
    public string? SafeErrorSummary { get; init; }
}
