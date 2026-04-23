using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tenant.Application.Interfaces;

namespace Tenant.Infrastructure.Services;

/// <summary>
/// TENANT-B12 — HTTP implementation of <see cref="IIdentityProvisioningAdapter"/>.
///
/// Calls the Identity internal provisioning endpoint
/// <c>POST /api/internal/tenant-provisioning/provision</c> to create the
/// auth/admin context for a tenant whose canonical record already exists
/// in the Tenant service DB.
///
/// Auth:    X-Provisioning-Token header sent to Identity.
///          When IdentityService:ProvisioningSecret is empty (dev), skipped.
/// Timeout: 30 s (provisioning includes DNS + product work).
/// Failure: never throws — returns IdentityProvisioningResult with Success=false.
/// </summary>
public class HttpIdentityProvisioningAdapter : IIdentityProvisioningAdapter
{
    private readonly IHttpClientFactory                          _httpClientFactory;
    private readonly IConfiguration                             _configuration;
    private readonly ILogger<HttpIdentityProvisioningAdapter>   _logger;

    public HttpIdentityProvisioningAdapter(
        IHttpClientFactory                          httpClientFactory,
        IConfiguration                             configuration,
        ILogger<HttpIdentityProvisioningAdapter>   logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration     = configuration;
        _logger            = logger;
    }

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

            return new IdentityProvisioningResult(
                Success:           true,
                AdminUserId:       TryGetString(root, "adminUserId"),
                AdminEmail:        TryGetString(root, "adminEmail"),
                TemporaryPassword: TryGetString(root, "temporaryPassword"),
                ProvisioningStatus: TryGetString(root, "provisioningStatus") ?? "Provisioned",
                Hostname:          TryGetString(root, "hostname"),
                Subdomain:         TryGetString(root, "subdomain"),
                Warnings:          [],
                Errors:            []);
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

    private static string? TryGetString(JsonElement root, string prop)
    {
        if (root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString();
        return null;
    }
}
