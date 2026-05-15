using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Contracts.Commerce;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Commerce;

/// <summary>
/// HTTP implementation of <see cref="ICommerceLifecycleNotifier"/> that
/// delivers <see cref="CommerceLifecycleEvent"/> payloads to the Commerce
/// service lifecycle ingest endpoint.
///
/// <para>
/// Endpoint called:
/// <c>POST /api/commerce/integration/lifecycle-events</c>
/// </para>
///
/// <para>
/// Registered automatically by
/// <see cref="CommerceIntegrationServiceCollectionExtensions.AddCommerceIntegration"/>
/// when <c>CommerceIntegration:Enabled = true</c>. The noop implementation
/// is used when <c>Enabled = false</c> (the default).
/// </para>
///
/// <para>
/// <b>Never throws.</b> All HTTP and serialization errors are caught and
/// logged at Warning level. Host business operations (tenant create, product
/// enable/disable) must never fail because Commerce delivery fails.
/// </para>
/// </summary>
internal sealed class HttpCommerceLifecycleNotifier : ICommerceLifecycleNotifier
{
    private readonly HttpClient                              _http;
    private readonly CommerceIntegrationOptions             _options;
    private readonly ILogger<HttpCommerceLifecycleNotifier> _logger;

    private const string IngestPath = "api/commerce/integration/lifecycle-events";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Converters                  = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public HttpCommerceLifecycleNotifier(
        HttpClient                               http,
        IOptions<CommerceIntegrationOptions>     options,
        ILogger<HttpCommerceLifecycleNotifier>   logger)
    {
        _http    = http;
        _options = options.Value;
        _logger  = logger;
    }

    /// <inheritdoc />
    public async Task NotifyAsync(CommerceLifecycleEvent ev, CancellationToken ct = default)
    {
        try
        {
            var json    = JsonSerializer.Serialize(ev, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug(
                "Commerce lifecycle notify: {EventType} tenant={ExternalTenantId} host={HostPlatformKey}",
                ev.EventType, ev.ExternalTenantId, ev.HostPlatformKey);

            var response = await _http.PostAsync(IngestPath, content, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Commerce lifecycle notify returned non-success {StatusCode} for {EventType} tenant={ExternalTenantId}",
                    (int)response.StatusCode, ev.EventType, ev.ExternalTenantId);
            }
            else
            {
                _logger.LogDebug(
                    "Commerce lifecycle notify accepted: {EventType} tenant={ExternalTenantId}",
                    ev.EventType, ev.ExternalTenantId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Commerce lifecycle notify failed for {EventType} tenant={ExternalTenantId} — delivery skipped, host operation unaffected",
                ev.EventType, ev.ExternalTenantId);
        }
    }
}
