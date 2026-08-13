using System.Net.Http.Headers;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Services;

public sealed class TenantServiceAuthDelegatingHandler : DelegatingHandler
{
    private const string SystemTenantId = "00000000-0000-0000-0000-000000000000";
    private const string TenantServiceAudience = "tenant-service";

    private readonly IServiceTokenIssuer _issuer;
    private readonly ILogger<TenantServiceAuthDelegatingHandler> _logger;

    public TenantServiceAuthDelegatingHandler(
        IServiceTokenIssuer issuer,
        ILogger<TenantServiceAuthDelegatingHandler> logger)
    {
        _issuer = issuer;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!_issuer.IsConfigured)
        {
            _logger.LogError(
                "Tenant Service discovery requires service-token configuration; request was not sent.");
            throw new InvalidOperationException(
                "Service-token signing is not configured for Tenant Service discovery.");
        }

        var token = _issuer.IssueToken(SystemTenantId, audience: TenantServiceAudience);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, cancellationToken);
    }
}
