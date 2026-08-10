using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Xenia.Application.Email;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email.Connectors;

/// <summary>
/// Generic IMAP email source connector.
///
/// Validates host, port, TLS requirements, and credential references.
/// Performs TCP probe to the configured IMAP host.
/// Enforces SSRF mitigation on custom hosts.
/// </summary>
internal sealed class ImapEmailConnector : IEmailSourceConnector
{
    private readonly ISecretReferenceService _secretService;
    private readonly ILogger<ImapEmailConnector> _logger;

    public ImapEmailConnector(
        ISecretReferenceService secretService,
        ILogger<ImapEmailConnector> logger)
    {
        _secretService = secretService;
        _logger = logger;
    }

    public EmailProviderType ProviderType => EmailProviderType.Imap;

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
                "IMAP_HOST_REQUIRED", "IMAP host is required.", (int)sw.ElapsedMilliseconds));

        var hostCheck = SsrfGuard.CheckHost(context.IncomingHost);
        if (!hostCheck.IsAllowed)
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.HostNotAllowed, "HOST_NOT_ALLOWED", hostCheck.SafeReason, (int)sw.ElapsedMilliseconds));

        if (!SupportedAuthTypes.Contains(context.AuthType))
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.ConfigurationInvalid,
                "AUTH_TYPE_UNSUPPORTED",
                $"Auth type '{context.AuthType}' is not supported for IMAP.", (int)sw.ElapsedMilliseconds));

        if (!context.UseTls)
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.TlsFailed,
                "TLS_REQUIRED",
                "IMAP connections require TLS. Plaintext IMAP is not permitted.", (int)sw.ElapsedMilliseconds));

        var port = context.IncomingPort ?? 993;
        if (port < 1 || port > 65535)
            return Task.FromResult(ConnectorValidationResult.Fail(
                EmailValidationResult.ConfigurationInvalid,
                "PORT_INVALID", "Port must be between 1 and 65535.", (int)sw.ElapsedMilliseconds));

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
                "SECRET_REF_REQUIRED", "A secret reference is required for IMAP authentication.", (int)sw.ElapsedMilliseconds);

        if (!_secretService.IsConfigured)
            return ConnectorValidationResult.Fail(
                EmailValidationResult.SecretUnavailable,
                "SECRET_SERVICE_UNAVAILABLE",
                "The secret reference service is not configured.", (int)sw.ElapsedMilliseconds);

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
                "IMAP connection timed out.", (int)sw.ElapsedMilliseconds);
        }
        catch
        {
            return ConnectorValidationResult.Fail(
                EmailValidationResult.ConnectionFailed, "CONNECTION_FAILED",
                "Could not connect to the IMAP server.", (int)sw.ElapsedMilliseconds);
        }
    }

    public ConnectorCapabilities GetCapabilities() => new()
    {
        ProviderType = EmailProviderType.Imap,
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
            ProviderType = EmailProviderType.Imap,
            IsReachable = false,
            SafeStatusDetail = "Diagnostic probe not invoked.",
            ResolvedHost = context.IncomingHost,
            ResolvedPort = context.IncomingPort ?? 993,
        });
}
