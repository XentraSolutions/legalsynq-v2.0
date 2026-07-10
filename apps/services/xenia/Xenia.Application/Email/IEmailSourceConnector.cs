using Xenia.Domain.Email;

namespace Xenia.Application.Email;

/// <summary>
/// Provider-neutral connector contract for email source operations.
///
/// Each supported provider type (Microsoft365, Google, IMAP, POP3, ExchangeIMAP)
/// must implement this interface. The connector registry maps provider types to
/// their connector implementations.
///
/// Connectors must NOT retrieve, download, or parse email messages.
/// This ticket covers connectivity validation only.
/// </summary>
public interface IEmailSourceConnector
{
    /// <summary>The provider type this connector handles.</summary>
    EmailProviderType ProviderType { get; }

    /// <summary>Supported authentication types for this provider.</summary>
    IReadOnlyList<EmailAuthType> SupportedAuthTypes { get; }

    /// <summary>
    /// Validates that the source configuration is structurally correct
    /// (required fields present, format valid). Does not make network calls.
    /// </summary>
    Task<ConnectorValidationResult> ValidateConfigurationAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Validates credentials by resolving the secret reference and verifying
    /// the credential format. May or may not make a lightweight network call.
    /// </summary>
    Task<ConnectorValidationResult> ValidateCredentialsAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Tests the full connection: configuration → credentials → network reachability → handshake.
    /// All results must be sanitized — no raw provider errors, no credential echoes.
    /// </summary>
    Task<ConnectorValidationResult> TestConnectionAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default);

    /// <summary>Returns the capabilities of this connector in the current environment.</summary>
    ConnectorCapabilities GetCapabilities();

    /// <summary>
    /// Returns safe diagnostic information (no secrets, no raw server banners).
    /// Used by the admin UI to surface status details.
    /// </summary>
    Task<ConnectorDiagnostics> GetSafeDiagnosticAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Context passed to connector operations — contains everything needed
/// to attempt a connection without accepting arbitrary caller-supplied data.
/// </summary>
public sealed record EmailSourceConnectorContext
{
    public required Guid SourceId { get; init; }
    public required Guid TenantId { get; init; }
    public required string EmailAddress { get; init; }
    public required EmailAuthType AuthType { get; init; }
    public string? Username { get; init; }
    public string? IncomingHost { get; init; }
    public int? IncomingPort { get; init; }
    public bool UseTls { get; init; } = true;
    public string? MailboxFolder { get; init; }
    public string? SecretReferenceId { get; init; }
    public string? OAuthConnectionRef { get; init; }
    public string? CorrelationId { get; init; }
    public string? ProviderConfigurationJson { get; init; }
}

/// <summary>Result of a connector operation.</summary>
public sealed record ConnectorValidationResult
{
    public required bool Success { get; init; }
    public required EmailValidationResult Result { get; init; }
    public required int DurationMs { get; init; }
    public string? ErrorCode { get; init; }
    public string? SafeErrorSummary { get; init; }

    public static ConnectorValidationResult Ok(int durationMs) => new()
    {
        Success = true,
        Result = EmailValidationResult.Success,
        DurationMs = durationMs,
    };

    public static ConnectorValidationResult Fail(
        EmailValidationResult result,
        string errorCode,
        string safeErrorSummary,
        int durationMs) => new()
    {
        Success = false,
        Result = result,
        DurationMs = durationMs,
        ErrorCode = errorCode,
        SafeErrorSummary = safeErrorSummary,
    };
}

/// <summary>Connector capability flags for the current environment.</summary>
public sealed record ConnectorCapabilities
{
    public required EmailProviderType ProviderType { get; init; }
    public required bool CanValidateConfiguration { get; init; }
    public required bool CanValidateCredentials { get; init; }
    public required bool CanTestConnection { get; init; }
    public required bool SupportsOAuth { get; init; }
    public required bool SupportsTls { get; init; }
    public required bool IsAvailableInEnvironment { get; init; }
    public string? UnavailableReason { get; init; }
}

/// <summary>Safe diagnostic snapshot from a connector. Contains no secrets.</summary>
public sealed record ConnectorDiagnostics
{
    public required EmailProviderType ProviderType { get; init; }
    public required bool IsReachable { get; init; }
    public string? SafeStatusDetail { get; init; }
    public string? ResolvedHost { get; init; }
    public int? ResolvedPort { get; init; }
    public bool? TlsNegotiated { get; init; }
    public int? LatencyMs { get; init; }
}
