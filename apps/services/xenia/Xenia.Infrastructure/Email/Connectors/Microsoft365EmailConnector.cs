using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Xenia.Application.Email;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email.Connectors;

/// <summary>
/// Email source connector for Microsoft 365 / Exchange Online.
///
/// Validates configuration and credential references without retrieving messages.
/// Full OAuth2 flow is not complete in this ticket (requires callback infrastructure).
/// Connection test performs a safe TCP/TLS probe to outlook.office365.com:993.
///
/// Limitations documented:
/// - Interactive OAuth2 completion requires XENIA-P1-T3 OAuth callback infrastructure.
/// - ClientCredentials flow requires Graph API registration — not wired in this environment.
/// </summary>
internal sealed class Microsoft365EmailConnector : IEmailSourceConnector
{
    private static readonly string[] _allowedHosts =
        ["outlook.office365.com", "outlook.office.com"];

    private readonly ISecretReferenceService _secretService;
    private readonly ILogger<Microsoft365EmailConnector> _logger;

    public Microsoft365EmailConnector(
        ISecretReferenceService secretService,
        ILogger<Microsoft365EmailConnector> logger)
    {
        _secretService = secretService;
        _logger = logger;
    }

    public EmailProviderType ProviderType => EmailProviderType.Microsoft365;

    public IReadOnlyList<EmailAuthType> SupportedAuthTypes =>
    [
        EmailAuthType.OAuth2,
        EmailAuthType.ClientCredentials,
        EmailAuthType.SecretReference,
    ];

    public Task<ConnectorValidationResult> ValidateConfigurationAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(context.EmailAddress))
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.ConfigurationInvalid,
                "EMAIL_ADDRESS_REQUIRED", "Email address is required.", (int)sw.ElapsedMilliseconds));

        if (!context.EmailAddress.Contains('@'))
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.ConfigurationInvalid,
                "EMAIL_ADDRESS_INVALID", "Email address format is invalid.", (int)sw.ElapsedMilliseconds));

        if (!SupportedAuthTypes.Contains(context.AuthType))
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.ConfigurationInvalid,
                "AUTH_TYPE_UNSUPPORTED",
                $"Auth type '{context.AuthType}' is not supported for Microsoft 365.",
                (int)sw.ElapsedMilliseconds));

        var host = context.IncomingHost ?? "outlook.office365.com";
        if (!_allowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Microsoft365 connector: non-standard host configured. SourceId={SourceId} Host={Host}",
                context.SourceId, host);
        }

        return Task.FromResult(ConnectorValidationResult.Ok((int)sw.ElapsedMilliseconds));
    }

    public async Task<ConnectorValidationResult> ValidateCredentialsAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (context.AuthType == EmailAuthType.OAuth2 || context.AuthType == EmailAuthType.ClientCredentials)
        {
            return ConnectorValidationResult.Fail(
                EmailValidationResult.ValidatorUnavailable,
                "OAUTH_NOT_WIRED",
                "OAuth2 credential validation requires OAuth callback infrastructure (XENIA-P1-T3).",
                (int)sw.ElapsedMilliseconds);
        }

        if (string.IsNullOrWhiteSpace(context.SecretReferenceId))
            return ConnectorValidationResult.Fail(
                EmailValidationResult.CredentialInvalid,
                "SECRET_REF_REQUIRED", "A secret reference is required.", (int)sw.ElapsedMilliseconds);

        if (!_secretService.IsValidReferenceFormat(context.SecretReferenceId))
            return ConnectorValidationResult.Fail(
                EmailValidationResult.CredentialInvalid,
                "SECRET_REF_INVALID", "Secret reference format is invalid.", (int)sw.ElapsedMilliseconds);

        if (!_secretService.IsConfigured)
            return ConnectorValidationResult.Fail(
                EmailValidationResult.SecretUnavailable,
                "SECRET_SERVICE_UNAVAILABLE",
                "The secret reference service is not configured in this environment.",
                (int)sw.ElapsedMilliseconds);

        var resolution = await _secretService.ResolveAsync(context.SecretReferenceId, ct);
        if (!resolution.Success)
            return ConnectorValidationResult.Fail(
                EmailValidationResult.SecretUnavailable,
                resolution.ErrorCode ?? "SECRET_UNRESOLVABLE",
                "Secret reference could not be resolved.",
                (int)sw.ElapsedMilliseconds);

        return ConnectorValidationResult.Ok((int)sw.ElapsedMilliseconds);
    }

    public async Task<ConnectorValidationResult> TestConnectionAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default)
    {
        var configResult = await ValidateConfigurationAsync(context, ct);
        if (!configResult.Success) return configResult;

        if (context.AuthType == EmailAuthType.OAuth2 || context.AuthType == EmailAuthType.ClientCredentials)
        {
            return ConnectorValidationResult.Fail(
                EmailValidationResult.ValidatorUnavailable,
                "OAUTH_NOT_WIRED",
                "Full connection test for OAuth2 requires OAuth callback infrastructure (XENIA-P1-T3). " +
                "Configuration validation passed.",
                0);
        }

        return await TcpTlsProbeAsync(context, ct);
    }

    public ConnectorCapabilities GetCapabilities() => new()
    {
        ProviderType = EmailProviderType.Microsoft365,
        CanValidateConfiguration = true,
        CanValidateCredentials = false,
        CanTestConnection = false,
        SupportsOAuth = true,
        SupportsTls = true,
        IsAvailableInEnvironment = true,
        UnavailableReason = "Full OAuth2 and connection test require XENIA-P1-T3 OAuth infrastructure.",
    };

    public Task<ConnectorDiagnostics> GetSafeDiagnosticAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default) =>
        Task.FromResult(new ConnectorDiagnostics
        {
            ProviderType = EmailProviderType.Microsoft365,
            IsReachable = false,
            SafeStatusDetail = "Diagnostic probe not available in this environment.",
        });

    private static async Task<ConnectorValidationResult> TcpTlsProbeAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var host = context.IncomingHost ?? "outlook.office365.com";
        var port = context.IncomingPort ?? 993;

        var hostCheckResult = SsrfGuard.CheckHost(host);
        if (!hostCheckResult.IsAllowed)
            return ConnectorValidationResult.Fail(
                EmailValidationResult.HostNotAllowed,
                "HOST_NOT_ALLOWED",
                hostCheckResult.SafeReason,
                (int)sw.ElapsedMilliseconds);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            using var tcpClient = new System.Net.Sockets.TcpClient();
            await tcpClient.ConnectAsync(host, port, timeoutCts.Token);

            return ConnectorValidationResult.Ok((int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return ConnectorValidationResult.Fail(
                EmailValidationResult.Timeout,
                "CONNECTION_TIMEOUT",
                $"Connection to mail server timed out after 10 seconds.",
                (int)sw.ElapsedMilliseconds);
        }
        catch
        {
            return ConnectorValidationResult.Fail(
                EmailValidationResult.ConnectionFailed,
                "CONNECTION_FAILED",
                "Could not establish a connection to the mail server.",
                (int)sw.ElapsedMilliseconds);
        }
    }
}
