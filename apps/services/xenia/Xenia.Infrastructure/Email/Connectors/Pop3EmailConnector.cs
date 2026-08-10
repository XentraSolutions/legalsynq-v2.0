using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Xenia.Application.Email;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email.Connectors;

/// <summary>
/// Generic POP3 email source connector.
///
/// TLS required (port 995). No folder support (POP3 has no mailbox folder concept).
/// </summary>
internal sealed class Pop3EmailConnector : IEmailSourceConnector
{
    private readonly ISecretReferenceService _secretService;
    private readonly ILogger<Pop3EmailConnector> _logger;

    public Pop3EmailConnector(
        ISecretReferenceService secretService,
        ILogger<Pop3EmailConnector> logger)
    {
        _secretService = secretService;
        _logger = logger;
    }

    public EmailProviderType ProviderType => EmailProviderType.Pop3;

    public IReadOnlyList<EmailAuthType> SupportedAuthTypes =>
    [
        EmailAuthType.UsernamePassword,
        EmailAuthType.AppPassword,
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
                "POP3_HOST_REQUIRED", "POP3 host is required.", (int)sw.ElapsedMilliseconds));

        var hostCheck = SsrfGuard.CheckHost(context.IncomingHost);
        if (!hostCheck.IsAllowed)
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.HostNotAllowed, "HOST_NOT_ALLOWED", hostCheck.SafeReason, (int)sw.ElapsedMilliseconds));

        if (!SupportedAuthTypes.Contains(context.AuthType))
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.ConfigurationInvalid,
                "AUTH_TYPE_UNSUPPORTED",
                $"Auth type '{context.AuthType}' is not supported for POP3.", (int)sw.ElapsedMilliseconds));

        if (!context.UseTls)
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.TlsFailed,
                "TLS_REQUIRED", "POP3 connections require TLS (port 995).", (int)sw.ElapsedMilliseconds));

        return Task.FromResult(ConnectorValidationResult.Ok((int)sw.ElapsedMilliseconds));
    }

    public async Task<ConnectorValidationResult> ValidateCredentialsAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(context.SecretReferenceId))
            return ConnectorValidationResult.Fail(
                EmailValidationResult.CredentialInvalid,
                "SECRET_REF_REQUIRED", "A secret reference is required.", (int)sw.ElapsedMilliseconds);

        if (!_secretService.IsConfigured)
            return ConnectorValidationResult.Fail(
                EmailValidationResult.SecretUnavailable,
                "SECRET_SERVICE_UNAVAILABLE",
                "Secret reference service is not configured.", (int)sw.ElapsedMilliseconds);

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

        var sw = Stopwatch.StartNew();
        var host = context.IncomingHost!;
        var port = context.IncomingPort ?? 995;

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
                "POP3 connection timed out.", (int)sw.ElapsedMilliseconds);
        }
        catch
        {
            return ConnectorValidationResult.Fail(
                EmailValidationResult.ConnectionFailed, "CONNECTION_FAILED",
                "Could not connect to the POP3 server.", (int)sw.ElapsedMilliseconds);
        }
    }

    public ConnectorCapabilities GetCapabilities() => new()
    {
        ProviderType = EmailProviderType.Pop3,
        CanValidateConfiguration = true,
        CanValidateCredentials = false,
        CanTestConnection = true,
        SupportsOAuth = false,
        SupportsTls = true,
        IsAvailableInEnvironment = true,
        UnavailableReason = null,
    };

    public Task<ConnectorDiagnostics> GetSafeDiagnosticAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default) =>
        Task.FromResult(new ConnectorDiagnostics
        {
            ProviderType = EmailProviderType.Pop3,
            IsReachable = false,
            SafeStatusDetail = "Diagnostic probe not invoked.",
            ResolvedHost = context.IncomingHost,
            ResolvedPort = context.IncomingPort ?? 995,
        });
}
