using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Xenia.Application.Email;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email.Connectors;

/// <summary>
/// Email source connector for Google Workspace / Gmail.
///
/// Supports OAuth2 (metadata only — full flow in T3) and App Password via IMAP.
/// Connection test performs a TCP/TLS probe to imap.gmail.com:993.
/// </summary>
internal sealed class GoogleEmailConnector : IEmailSourceConnector
{
    private readonly ISecretReferenceService _secretService;
    private readonly ILogger<GoogleEmailConnector> _logger;

    public GoogleEmailConnector(
        ISecretReferenceService secretService,
        ILogger<GoogleEmailConnector> logger)
    {
        _secretService = secretService;
        _logger = logger;
    }

    public EmailProviderType ProviderType => EmailProviderType.Google;

    public IReadOnlyList<EmailAuthType> SupportedAuthTypes =>
    [
        EmailAuthType.OAuth2,
        EmailAuthType.AppPassword,
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

        if (!SupportedAuthTypes.Contains(context.AuthType))
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.ConfigurationInvalid,
                "AUTH_TYPE_UNSUPPORTED",
                $"Auth type '{context.AuthType}' is not supported for Google.", (int)sw.ElapsedMilliseconds));

        if (!context.UseTls)
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.TlsFailed,
                "TLS_REQUIRED", "Google IMAP requires TLS. UseTls must be true.", (int)sw.ElapsedMilliseconds));

        return Task.FromResult(ConnectorValidationResult.Ok((int)sw.ElapsedMilliseconds));
    }

    public async Task<ConnectorValidationResult> ValidateCredentialsAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (context.AuthType == EmailAuthType.OAuth2)
            return ConnectorValidationResult.Fail(
                EmailValidationResult.ValidatorUnavailable,
                "OAUTH_NOT_WIRED",
                "OAuth2 credential validation requires OAuth callback infrastructure (XENIA-P1-T3).",
                (int)sw.ElapsedMilliseconds);

        if (string.IsNullOrWhiteSpace(context.SecretReferenceId))
            return ConnectorValidationResult.Fail(
                EmailValidationResult.CredentialInvalid,
                "SECRET_REF_REQUIRED", "A secret reference is required.", (int)sw.ElapsedMilliseconds);

        if (!_secretService.IsConfigured)
            return ConnectorValidationResult.Fail(
                EmailValidationResult.SecretUnavailable,
                "SECRET_SERVICE_UNAVAILABLE",
                "The secret reference service is not configured in this environment.",
                (int)sw.ElapsedMilliseconds);

        var resolution = await _secretService.ResolveAsync(context.SecretReferenceId, ct);
        return resolution.Success
            ? ConnectorValidationResult.Ok((int)sw.ElapsedMilliseconds)
            : ConnectorValidationResult.Fail(
                EmailValidationResult.SecretUnavailable,
                resolution.ErrorCode ?? "SECRET_UNRESOLVABLE",
                "Secret reference could not be resolved.", (int)sw.ElapsedMilliseconds);
    }

    public async Task<ConnectorValidationResult> TestConnectionAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default)
    {
        var configResult = await ValidateConfigurationAsync(context, ct);
        if (!configResult.Success) return configResult;

        if (context.AuthType == EmailAuthType.OAuth2)
            return ConnectorValidationResult.Fail(
                EmailValidationResult.ValidatorUnavailable,
                "OAUTH_NOT_WIRED",
                "Full connection test for OAuth2 requires XENIA-P1-T3 OAuth infrastructure.", 0);

        var sw = Stopwatch.StartNew();
        var host = context.IncomingHost ?? "imap.gmail.com";
        var port = context.IncomingPort ?? 993;
        var hostCheck = SsrfGuard.CheckHost(host);
        if (!hostCheck.IsAllowed)
            return ConnectorValidationResult.Fail(
                EmailValidationResult.HostNotAllowed, "HOST_NOT_ALLOWED", hostCheck.SafeReason, (int)sw.ElapsedMilliseconds);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            using var tcp = new System.Net.Sockets.TcpClient();
            await tcp.ConnectAsync(host, port, timeoutCts.Token);
            return ConnectorValidationResult.Ok((int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return ConnectorValidationResult.Fail(
                EmailValidationResult.Timeout, "CONNECTION_TIMEOUT",
                "Connection to Google mail server timed out.", (int)sw.ElapsedMilliseconds);
        }
        catch
        {
            return ConnectorValidationResult.Fail(
                EmailValidationResult.ConnectionFailed, "CONNECTION_FAILED",
                "Could not connect to Google mail server.", (int)sw.ElapsedMilliseconds);
        }
    }

    public ConnectorCapabilities GetCapabilities() => new()
    {
        ProviderType = EmailProviderType.Google,
        CanValidateConfiguration = true,
        CanValidateCredentials = false,
        CanTestConnection = true,
        SupportsOAuth = true,
        SupportsTls = true,
        IsAvailableInEnvironment = true,
        UnavailableReason = null,
    };

    public Task<ConnectorDiagnostics> GetSafeDiagnosticAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default) =>
        Task.FromResult(new ConnectorDiagnostics
        {
            ProviderType = EmailProviderType.Google,
            IsReachable = false,
            SafeStatusDetail = "Diagnostic probe not invoked.",
        });
}
