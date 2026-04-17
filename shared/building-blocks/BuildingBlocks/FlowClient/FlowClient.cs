using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-MERGE-P4 — typed <see cref="HttpClient"/> implementation of
/// <see cref="IFlowClient"/>. Forwards the caller's <c>Authorization</c>
/// header so Flow's per-product capability policies still apply, logs at
/// the boundary, and surfaces transport failures as
/// <see cref="FlowClientUnavailableException"/>.
/// </summary>
internal sealed class FlowClient : IFlowClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<FlowClient> _logger;

    public FlowClient(HttpClient http, IHttpContextAccessor httpContextAccessor, ILogger<FlowClient> logger)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<FlowProductWorkflowResponse> StartWorkflowAsync(
        string productSlug,
        StartProductWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = $"/api/v1/product-workflows/{productSlug}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        ApplyBearer(httpRequest);

        _logger.LogInformation(
            "FlowClient → POST {Path} entity={EntityType}/{EntityId} definition={DefinitionId}",
            path, request.SourceEntityType, request.SourceEntityId, request.WorkflowDefinitionId);

        var response = await SendAsync(httpRequest, cancellationToken);
        var dto = await ReadJsonAsync<FlowProductWorkflowResponse>(response, cancellationToken);
        if (dto is null)
        {
            throw new FlowClientUnavailableException("Flow returned an empty body for StartWorkflow.");
        }
        return dto;
    }

    public async Task<IReadOnlyList<FlowProductWorkflowResponse>> ListBySourceEntityAsync(
        string productSlug,
        string sourceEntityType,
        string sourceEntityId,
        CancellationToken cancellationToken = default)
    {
        var qs = $"?sourceEntityType={Uri.EscapeDataString(sourceEntityType)}&sourceEntityId={Uri.EscapeDataString(sourceEntityId)}";
        var path = $"/api/v1/product-workflows/{productSlug}{qs}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyBearer(httpRequest);

        _logger.LogInformation(
            "FlowClient → GET {Path} entity={EntityType}/{EntityId}",
            path, sourceEntityType, sourceEntityId);

        var response = await SendAsync(httpRequest, cancellationToken);
        var list = await ReadJsonAsync<List<FlowProductWorkflowResponse>>(response, cancellationToken);
        return list ?? new List<FlowProductWorkflowResponse>();
    }

    private void ApplyBearer(HttpRequestMessage httpRequest)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null) return;
        var auth = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth)) return;

        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", auth.Substring("Bearer ".Length).Trim());
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "FlowClient ← {Status} {Method} {Uri} body={Body}",
                    (int)response.StatusCode, request.Method, request.RequestUri, Truncate(body, 512));

                // 4xx propagates the upstream HTTP error so policy denials
                // (401/403) and validation (400) reach the caller meaningfully.
                throw new HttpRequestException(
                    $"Flow returned {(int)response.StatusCode} for {request.Method} {request.RequestUri}: {Truncate(body, 256)}",
                    inner: null,
                    statusCode: response.StatusCode);
            }
            return response;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            _logger.LogError(ex, "FlowClient transport failure for {Method} {Uri}", request.Method, request.RequestUri);
            throw new FlowClientUnavailableException("Flow service is unreachable.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "FlowClient timeout for {Method} {Uri}", request.Method, request.RequestUri);
            throw new FlowClientUnavailableException("Flow service request timed out.", ex);
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            throw new FlowClientUnavailableException("Flow returned a malformed response body.", ex);
        }
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value.Substring(0, max) + "…";
}
