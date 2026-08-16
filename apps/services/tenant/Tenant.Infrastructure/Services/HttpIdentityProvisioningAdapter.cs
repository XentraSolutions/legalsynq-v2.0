using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tenant.Application.Interfaces;

namespace Tenant.Infrastructure.Services;

/// <summary>
/// TENANT-B12 — HTTP implementation of <see cref="IIdentityProvisioningAdapter"/>.
/// TENANT-STABILIZATION — Extended with RetryProvisioningAsync and RetryVerificationAsync.
///
/// Calls the Identity admin/internal endpoints for:
///   - Initial provisioning: POST /api/internal/tenant-provisioning/provision
///   - Retry provisioning:   POST /api/admin/tenants/{id}/provisioning/retry
///   - Retry verification:   POST /api/admin/tenants/{id}/verification/retry
///
    /// Auth:    X-Provisioning-Token header sent to Identity for internal provisioning.
    ///          Retry endpoints forward the platform admin Authorization header.
/// Timeout: 30 s for initial provisioning; 15 s for retries.
/// Failure: never throws — returns result with Success=false.
/// </summary>
public class HttpIdentityProvisioningAdapter : IIdentityProvisioningAdapter
{
    private readonly IHttpClientFactory                          _httpClientFactory;
    private readonly IHttpContextAccessor                        _httpContextAccessor;
    private readonly IConfiguration                             _configuration;
    private readonly ILogger<HttpIdentityProvisioningAdapter>   _logger;

    public HttpIdentityProvisioningAdapter(
        IHttpClientFactory                          httpClientFactory,
        IHttpContextAccessor                        httpContextAccessor,
        IConfiguration                             configuration,
        ILogger<HttpIdentityProvisioningAdapter>   logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _configuration     = configuration;
        _logger            = logger;
    }

    public async Task<IdentityEmailAvailabilityResult> CheckAdminEmailAsync(
        string email,
        CancellationToken ct = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("IdentityInternal");
            var secret = _configuration["IdentityService:ProvisioningSecret"];
            if (!string.IsNullOrWhiteSpace(secret))
                client.DefaultRequestHeaders.Add("X-Provisioning-Token", secret);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            using var response = await client.GetAsync(
                $"/api/internal/users/account-exists?email={Uri.EscapeDataString(email)}",
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[IdentityProvisioning] Admin email availability returned {StatusCode}",
                    (int)response.StatusCode);
                return new(false, false);
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cts.Token));
            return new(true, TryGetBool(doc.RootElement, "exists"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[IdentityProvisioning] Admin email availability check failed");
            return new(false, false);
        }
    }

    // ── Initial provisioning ──────────────────────────────────────────────────

    public async Task<IdentityProvisioningResult> ProvisionAsync(
        IdentityProvisioningRequest request,
        CancellationToken           ct = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("IdentityInternal");

            var secret = _configuration["IdentityService:ProvisioningSecret"];
            if (!string.IsNullOrWhiteSpace(secret))
                client.DefaultRequestHeaders.Add("X-Provisioning-Token", secret);

            var payload = new
            {
                tenantId       = request.TenantId,
                code           = request.Code,
                displayName    = request.DisplayName,
                orgType        = request.OrgType,
                adminEmail     = request.AdminEmail,
                adminFirstName = request.AdminFirstName,
                adminLastName  = request.AdminLastName,
                subdomain      = request.PreferredSubdomain,
                addressLine1   = request.AddressLine1,
                city           = request.City,
                state          = request.State,
                postalCode     = request.PostalCode,
                latitude       = request.Latitude,
                longitude      = request.Longitude,
                geoPointSource = request.GeoPointSource,
                products       = request.Products ?? [],
                tenantRegistrationApproval = request.TenantRegistrationApproval,
            };

            var json    = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var response = await client.PostAsync(
                "/api/internal/tenant-provisioning/provision",
                content,
                cts.Token);

            var body = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[IdentityProvisioning] Provisioning returned {StatusCode} for TenantId={TenantId}: {Body}",
                    (int)response.StatusCode, request.TenantId, body);

                return new IdentityProvisioningResult(
                    Success:           false,
                    AdminUserId:       null,
                    AdminEmail:        null,
                    TemporaryPassword: null,
                    ProvisioningStatus: "Failed",
                    Hostname:          null,
                    Subdomain:         null,
                    Warnings:          [],
                    Errors:            [$"Identity returned HTTP {(int)response.StatusCode}: {body}"]);
            }

            using var doc = JsonDocument.Parse(body);
            var root      = doc.RootElement;
            var explicitSuccess = !root.TryGetProperty("success", out var successElement) || successElement.GetBoolean();
            var warnings = ReadStringArray(root, "warnings");
            var errors = ReadStringArray(root, "errors");

            return new IdentityProvisioningResult(
                Success:           explicitSuccess,
                AdminUserId:       TryGetString(root, "adminUserId"),
                AdminEmail:        TryGetString(root, "adminEmail"),
                TemporaryPassword: TryGetString(root, "temporaryPassword"),
                ProvisioningStatus: TryGetString(root, "provisioningStatus") ?? "Provisioned",
                Hostname:          TryGetString(root, "hostname"),
                Subdomain:         TryGetString(root, "subdomain"),
                Warnings:          warnings,
                Errors:            errors);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "[IdentityProvisioning] Timeout provisioning TenantId={TenantId}", request.TenantId);

            return new IdentityProvisioningResult(
                Success:           false,
                AdminUserId:       null,
                AdminEmail:        null,
                TemporaryPassword: null,
                ProvisioningStatus: "Failed",
                Hostname:          null,
                Subdomain:         null,
                Warnings:          [],
                Errors:            ["Identity provisioning timed out (30s)."]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[IdentityProvisioning] Unexpected failure provisioning TenantId={TenantId}", request.TenantId);

            return new IdentityProvisioningResult(
                Success:           false,
                AdminUserId:       null,
                AdminEmail:        null,
                TemporaryPassword: null,
                ProvisioningStatus: "Failed",
                Hostname:          null,
                Subdomain:         null,
                Warnings:          [],
                Errors:            [$"Identity provisioning error: {ex.Message}"]);
        }
    }

    private static List<string> ReadStringArray(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array) return [];
        return value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
    }

    // ── Retry provisioning proxy ──────────────────────────────────────────────

    public async Task<ProvisioningRetryResult> RetryProvisioningAsync(
        Guid              tenantId,
        CancellationToken ct = default)
    {
        return await ProxyRetryAsync(
            tenantId,
            $"/api/admin/tenants/{tenantId}/provisioning/retry",
            "RetryProvisioning",
            ct);
    }

    // ── Retry verification proxy ──────────────────────────────────────────────

    public async Task<ProvisioningRetryResult> RetryVerificationAsync(
        Guid              tenantId,
        CancellationToken ct = default)
    {
        return await ProxyRetryAsync(
            tenantId,
            $"/api/admin/tenants/{tenantId}/verification/retry",
            "RetryVerification",
            ct);
    }

    // ── Shared proxy helper ───────────────────────────────────────────────────

    private async Task<ProvisioningRetryResult> ProxyRetryAsync(
        Guid              tenantId,
        string            path,
        string            operationName,
        CancellationToken ct)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("IdentityInternal");
            AddAuthorizationHeader(client);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var content  = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await client.PostAsync(path, content, cts.Token);
            var body     = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[IdentityProvisioning] {Operation} returned {StatusCode} for TenantId={TenantId}: {Body}",
                    operationName, (int)response.StatusCode, tenantId, body);

                return new ProvisioningRetryResult(
                    Success:           false,
                    ProvisioningStatus: "Unknown",
                    Hostname:           null,
                    FailureStage:       null,
                    Error:              $"Identity returned HTTP {(int)response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            var root      = doc.RootElement;

            return new ProvisioningRetryResult(
                Success:           TryGetBool(root, "success"),
                ProvisioningStatus: TryGetString(root, "provisioningStatus") ?? "Unknown",
                Hostname:           TryGetString(root, "hostname"),
                FailureStage:       TryGetString(root, "failureStage"),
                Error:              TryGetString(root, "error"),
                AttemptNumber:      TryGetInt(root, "attemptNumber"),
                StillRetrying:      TryGetNullableBool(root, "stillRetrying"),
                Exhausted:          TryGetNullableBool(root, "exhausted"),
                NextRetryAtUtc:     TryGetDateTime(root, "nextRetryAtUtc"));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "[IdentityProvisioning] {Operation} timed out for TenantId={TenantId}",
                operationName, tenantId);

            return new ProvisioningRetryResult(
                Success:           false,
                ProvisioningStatus: "Unknown",
                Hostname:           null,
                FailureStage:       null,
                Error:              $"{operationName} timed out (15s).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[IdentityProvisioning] {Operation} unexpected failure for TenantId={TenantId}",
                operationName, tenantId);

            return new ProvisioningRetryResult(
                Success:           false,
                ProvisioningStatus: "Unknown",
                Hostname:           null,
                FailureStage:       null,
                Error:              $"{operationName} error: {ex.Message}");
        }
    }

    // ── JSON helpers ──────────────────────────────────────────────────────────

    private void AddAuthorizationHeader(HttpClient client)
    {
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
        }
    }

    private static string? TryGetString(JsonElement root, string prop)
    {
        if (root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString();
        return null;
    }

    private static bool TryGetBool(JsonElement root, string prop)
    {
        if (root.TryGetProperty(prop, out var el))
        {
            if (el.ValueKind == JsonValueKind.True)  return true;
            if (el.ValueKind == JsonValueKind.False) return false;
        }
        return false;
    }

    private static bool? TryGetNullableBool(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var el)) return null;
        if (el.ValueKind == JsonValueKind.True) return true;
        if (el.ValueKind == JsonValueKind.False) return false;
        return null;
    }

    private static int? TryGetInt(JsonElement root, string prop)
    {
        if (root.TryGetProperty(prop, out var el) &&
            el.ValueKind == JsonValueKind.Number &&
            el.TryGetInt32(out var value))
            return value;
        return null;
    }

    private static DateTime? TryGetDateTime(JsonElement root, string prop)
    {
        if (root.TryGetProperty(prop, out var el) &&
            el.ValueKind == JsonValueKind.String &&
            el.TryGetDateTime(out var value))
            return value;
        return null;
    }
}
