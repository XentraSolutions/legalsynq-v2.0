using System.Net;
using System.Net.Http.Headers;
using Identity.Application.Interfaces;
using Identity.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

public sealed class TenantVerificationService : ITenantVerificationService
{
    private const string CanonicalVerificationEndpointPath = "/.well-known/tenant-verify";
    private const string LegacyVerificationEndpointPath = "/.well-known/legalsynq-tenant-verification";

    private readonly TenantVerificationOptions _opts;
    private readonly IDnsService _dns;
    private readonly ILogger<TenantVerificationService> _log;

    public TenantVerificationService(
        IOptions<TenantVerificationOptions> opts,
        IDnsService dns,
        ILogger<TenantVerificationService> log)
    {
        _opts = opts.Value;
        _dns = dns;
        _log = log;
    }

    public async Task<VerificationResult> VerifyAsync(Tenant tenant, string hostname, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
        {
            _log.LogInformation(
                "Verification disabled — auto-passing for tenant {TenantCode} hostname {Hostname}",
                tenant.Code, hostname);
            return new VerificationResult(true, null);
        }

        if (_opts.DevBypass)
        {
            _log.LogInformation(
                "Verification dev-bypass enabled — auto-passing for tenant {TenantCode} hostname {Hostname}",
                tenant.Code, hostname);
            return new VerificationResult(true, null);
        }

        var dnsResult = await VerifyDnsAsync(hostname, ct);
        if (!dnsResult.Success)
        {
            var authoritativeDnsResult = await VerifyAuthoritativeDnsAsync(hostname, dnsResult.ErrorMessage, ct);
            if (authoritativeDnsResult.Success)
            {
                return authoritativeDnsResult;
            }

            return dnsResult;
        }

        var httpResult = await VerifyHttpAsync(hostname, tenant.Code, ct);
        return httpResult;
    }

    private async Task<VerificationResult> VerifyDnsAsync(string hostname, CancellationToken ct)
    {
        _log.LogInformation("DNS verification started for {Hostname}", hostname);

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(hostname, ct)
                .WaitAsync(TimeSpan.FromSeconds(_opts.DnsTimeoutSeconds), ct);

            if (addresses.Length == 0)
            {
                var msg = $"DNS resolution returned no addresses for '{hostname}'.";
                _log.LogWarning("DNS verification failed for {Hostname}: {Reason}", hostname, msg);
                return new VerificationResult(false, msg, ProvisioningFailureStage.DnsVerification);
            }

            _log.LogInformation(
                "DNS verification passed for {Hostname}: resolved to {Addresses}",
                hostname, string.Join(", ", addresses.Select(a => a.ToString())));

            return new VerificationResult(true, null);
        }
        catch (OperationCanceledException)
        {
            var msg = $"DNS resolution timed out for '{hostname}' after {_opts.DnsTimeoutSeconds}s.";
            _log.LogWarning("DNS verification failed for {Hostname}: {Reason}", hostname, msg);
            return new VerificationResult(false, msg, ProvisioningFailureStage.DnsVerification);
        }
        catch (Exception ex)
        {
            var msg = $"DNS resolution failed for '{hostname}': {ex.Message}";
            _log.LogWarning(ex, "DNS verification failed for {Hostname}", hostname);
            return new VerificationResult(false, msg.Length > 500 ? msg[..500] : msg, ProvisioningFailureStage.DnsVerification);
        }
    }

    private async Task<VerificationResult> VerifyAuthoritativeDnsAsync(
        string hostname,
        string? resolverFailureReason,
        CancellationToken ct)
    {
        if (!await _dns.RecordExistsAsync(hostname, ct))
            return new VerificationResult(false, resolverFailureReason, ProvisioningFailureStage.DnsVerification);

        _log.LogWarning(
            "Recursive DNS verification failed for {Hostname}, but Route53 authoritative record exists; accepting DNS verification. Resolver reason: {Reason}",
            hostname, resolverFailureReason);

        return new VerificationResult(true, null);
    }

    private async Task<VerificationResult> VerifyHttpAsync(string hostname, string tenantCode, CancellationToken ct)
    {
        _log.LogInformation("HTTP/app verification started for {Hostname}", hostname);

        var verificationEndpointPath = GetVerificationEndpointPath();
        var url = $"https://{hostname}{verificationEndpointPath}";

        try
        {
            using var client = new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(_opts.HttpTimeoutSeconds)
            };
            client.DefaultRequestHeaders.Host = hostname;

            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var msg = $"HTTP verification returned status {(int)response.StatusCode} for '{url}'.";
                _log.LogWarning("HTTP verification failed for {Hostname}: {Reason}", hostname, msg);
                return new VerificationResult(false, msg, ProvisioningFailureStage.HttpVerification);
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            if (!body.Contains("tenant-verify-ok", StringComparison.OrdinalIgnoreCase))
            {
                var msg = $"HTTP verification response did not contain expected verification token for '{hostname}'.";
                _log.LogWarning("HTTP verification failed for {Hostname}: {Reason}", hostname, msg);
                return new VerificationResult(false, msg, ProvisioningFailureStage.HttpVerification);
            }

            _log.LogInformation("HTTP/app verification passed for {Hostname}", hostname);
            return new VerificationResult(true, null);
        }
        catch (TaskCanceledException)
        {
            var msg = $"HTTP verification timed out for '{hostname}' after {_opts.HttpTimeoutSeconds}s.";
            _log.LogWarning("HTTP verification failed for {Hostname}: {Reason}", hostname, msg);
            return new VerificationResult(false, msg, ProvisioningFailureStage.HttpVerification);
        }
        catch (HttpRequestException ex)
        {
            var msg = $"HTTP verification failed for '{hostname}': {ex.Message}";
            _log.LogWarning(ex, "HTTP verification failed for {Hostname}", hostname);
            return new VerificationResult(false, msg.Length > 500 ? msg[..500] : msg, ProvisioningFailureStage.HttpVerification);
        }
        catch (Exception ex)
        {
            var msg = $"HTTP verification error for '{hostname}': {ex.Message}";
            _log.LogWarning(ex, "HTTP verification error for {Hostname}", hostname);
            return new VerificationResult(false, msg.Length > 500 ? msg[..500] : msg, ProvisioningFailureStage.HttpVerification);
        }
    }

    private string GetVerificationEndpointPath()
    {
        var path = string.IsNullOrWhiteSpace(_opts.VerificationEndpointPath)
            ? CanonicalVerificationEndpointPath
            : _opts.VerificationEndpointPath.Trim();

        if (!path.StartsWith('/'))
            path = "/" + path;

        if (string.Equals(path, LegacyVerificationEndpointPath, StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning(
                "Tenant verification endpoint path {LegacyPath} is deprecated; using {CanonicalPath}",
                path, CanonicalVerificationEndpointPath);
            return CanonicalVerificationEndpointPath;
        }

        return path;
    }
}
