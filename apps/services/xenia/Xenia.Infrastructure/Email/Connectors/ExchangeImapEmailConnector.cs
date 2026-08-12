using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Xenia.Application.Email;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email.Connectors;

/// <summary>
/// Exchange Server (IMAP) email source connector.
///
/// Covers on-premises and hybrid Exchange deployments accessed via IMAP.
/// Supports OAuth2 (metadata only — T3) and UsernamePassword (secret reference).
/// </summary>
internal sealed class ExchangeImapEmailConnector : IEmailSourceConnector
{
    private readonly ISecretReferenceService _secretService;
    private readonly ILogger<ExchangeImapEmailConnector> _logger;

    public ExchangeImapEmailConnector(
        ISecretReferenceService secretService,
        ILogger<ExchangeImapEmailConnector> logger)
    {
        _secretService = secretService;
        _logger = logger;
    }

    public EmailProviderType ProviderType => EmailProviderType.ExchangeImap;

    public IReadOnlyList<EmailAuthType> SupportedAuthTypes =>
    [
        EmailAuthType.OAuth2,
        EmailAuthType.UsernamePassword,
        EmailAuthType.SecretReference,
    ];

    public Task<ConnectorValidationResult> ValidateConfigurationAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(context.IncomingHost))
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.ConfigurationInvalid,
                "EXCHANGE_HOST_REQUIRED",
                "Exchange IMAP host is required.", (int)sw.ElapsedMilliseconds));

        var hostCheck = SsrfGuard.CheckHost(context.IncomingHost);
        if (!hostCheck.IsAllowed)
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.HostNotAllowed, "HOST_NOT_ALLOWED", hostCheck.SafeReason, (int)sw.ElapsedMilliseconds));

        if (!SupportedAuthTypes.Contains(context.AuthType))
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.ConfigurationInvalid,
                "AUTH_TYPE_UNSUPPORTED",
                $"Auth type '{context.AuthType}' is not supported for Exchange IMAP.", (int)sw.ElapsedMilliseconds));

        if (!context.UseTls)
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.TlsFailed,
                "TLS_REQUIRED",
                "Exchange IMAP requires TLS.", (int)sw.ElapsedMilliseconds));

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
                "OAuth2 for Exchange requires XENIA-P1-T3 OAuth infrastructure.",
                (int)sw.ElapsedMilliseconds);

        if (string.IsNullOrWhiteSpace(context.SecretReferenceId))
            return ConnectorValidationResult.Fail(
                EmailValidationResult.CredentialInvalid,
                "SECRET_REF_REQUIRED", "A secret reference is required.", (int)sw.ElapsedMilliseconds);

        if (!_secretService.IsConfigured)
            return ConnectorValidationResult.Fail(
                EmailValidationResult.SecretUnavailable,
                "SECRET_SERVICE_UNAVAILABLE",
                "Secret reference service not configured.", (int)sw.ElapsedMilliseconds);

        var res = await _secretService.ResolveAsync(context.SecretReferenceId, ct);
        return res.Success
            ? ConnectorValidationResult.Ok((int)sw.ElapsedMilliseconds)
            : ConnectorValidationResult.Fail(
                EmailValidationResult.SecretUnavailable,
                res.ErrorCode ?? "SECRET_UNRESOLVABLE",
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
                "Full connection test for OAuth2 requires XENIA-P1-T3 infrastructure.", 0);

        var sw = Stopwatch.StartNew();
        var host = context.IncomingHost!;
        var port = context.IncomingPort ?? 993;

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
                "Exchange IMAP connection timed out.", (int)sw.ElapsedMilliseconds);
        }
        catch
        {
            return ConnectorValidationResult.Fail(
                EmailValidationResult.ConnectionFailed, "CONNECTION_FAILED",
                "Could not connect to the Exchange IMAP server.", (int)sw.ElapsedMilliseconds);
        }
    }

    public ConnectorCapabilities GetCapabilities() => new()
    {
        ProviderType = EmailProviderType.ExchangeImap,
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
            ProviderType = EmailProviderType.ExchangeImap,
            IsReachable = false,
            SafeStatusDetail = "Diagnostic probe not invoked.",
            ResolvedHost = context.IncomingHost,
            ResolvedPort = context.IncomingPort ?? 993,
        });
}
