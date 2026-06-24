using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Tenant.Application.Interfaces;

namespace Tenant.Infrastructure.Services;

/// <summary>
/// TENANT-B11 — HTTP read-through to Identity for compat data.
/// TENANT-STABILIZATION — Extended with SetSessionTimeoutAsync proxy.
///
/// Reads Identity-owned fields (e.g. sessionTimeoutMinutes) via the Identity
/// admin endpoint and surfaces them in the Tenant admin aggregate response.
///
/// All operations are best-effort: a timeout, non-success status, or
/// deserialisation failure returns null/false instead of throwing.
/// </summary>
public class HttpIdentityCompatAdapter : IIdentityCompatAdapter
{
    private readonly IHttpClientFactory                  _httpClientFactory;
    private readonly IHttpContextAccessor                _httpContextAccessor;
    private readonly ILogger<HttpIdentityCompatAdapter>  _logger;

    public HttpIdentityCompatAdapter(
        IHttpClientFactory                  httpClientFactory,
        IHttpContextAccessor                httpContextAccessor,
        ILogger<HttpIdentityCompatAdapter>  logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger            = logger;
    }

    // ── Read: session timeout ─────────────────────────────────────────────────

    public async Task<int?> GetSessionTimeoutMinutesAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("IdentityInternal");
            AddAuthorizationHeader(client);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var response = await client.GetAsync(
                $"/api/admin/tenants/{tenantId}",
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "IdentityCompatAdapter: GET /api/admin/tenants/{TenantId} returned {StatusCode} — returning null",
                    tenantId, (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(body);
            var root    = doc.RootElement;

            if (root.TryGetProperty("sessionTimeoutMinutes", out var prop) &&
                prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "IdentityCompatAdapter: timed out reading sessionTimeoutMinutes for tenant {TenantId}",
                tenantId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "IdentityCompatAdapter: failed reading sessionTimeoutMinutes for tenant {TenantId}",
                tenantId);
            return null;
        }
    }

    // ── Write: session timeout proxy ──────────────────────────────────────────

    public async Task<bool> SetSessionTimeoutAsync(
        Guid              tenantId,
        int?              sessionTimeoutMinutes,
        CancellationToken ct = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("IdentityInternal");
            AddAuthorizationHeader(client);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var payload = JsonSerializer.Serialize(new { sessionTimeoutMinutes });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PatchAsync(
                $"/api/admin/tenants/{tenantId}/session-settings",
                content,
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "IdentityCompatAdapter: PATCH session-settings for tenant {TenantId} returned {StatusCode}",
                    tenantId, (int)response.StatusCode);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "IdentityCompatAdapter: timed out setting sessionTimeout for tenant {TenantId}",
                tenantId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "IdentityCompatAdapter: failed setting sessionTimeout for tenant {TenantId}",
                tenantId);
            return false;
        }
    }

    public async Task<bool> SetTenantProductEntitlementAsync(
        Guid              tenantId,
        string            productCode,
        bool              enabled,
        CancellationToken ct = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("IdentityInternal");
            AddAuthorizationHeader(client);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var payload = JsonSerializer.Serialize(new { enabled });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                $"/api/admin/tenants/{tenantId}/entitlements/{Uri.EscapeDataString(productCode)}",
                content,
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "IdentityCompatAdapter: POST entitlement for tenant {TenantId} product {ProductCode} returned {StatusCode}",
                    tenantId, productCode, (int)response.StatusCode);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "IdentityCompatAdapter: timed out setting entitlement for tenant {TenantId} product {ProductCode}",
                tenantId, productCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "IdentityCompatAdapter: failed setting entitlement for tenant {TenantId} product {ProductCode}",
                tenantId, productCode);
            return false;
        }
    }

    private void AddAuthorizationHeader(HttpClient client)
    {
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
        }
    }
}
