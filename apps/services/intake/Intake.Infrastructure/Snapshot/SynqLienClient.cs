using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Authentication.ServiceTokens;
using Intake.Application.Snapshot;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Snapshot;

public sealed class SynqLienClient(
    HttpClient httpClient,
    IServiceTokenIssuer tokenIssuer,
    SynqLienDestinationOptions options,
    ILogger<SynqLienClient> logger) : ISynqLienClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<SynqLienCallResult<SynqLienCaseResponse>> GetCaseAsync(
        Guid tenantId, Guid caseId, string correlationId, CancellationToken cancellationToken) =>
        SendAsync<SynqLienCaseResponse>(
            HttpMethod.Get, $"/api/internal/synqlien/cases/{caseId}", tenantId, Guid.Empty, null,
            correlationId, string.Empty, cancellationToken);

    public Task<SynqLienCallResult<SynqLienCaseResponse>> CreateCaseAsync(
        Guid tenantId, Guid actingUserId, string idempotencyKey, string correlationId,
        SynqLienCaseRequest request, CancellationToken cancellationToken) =>
        SendAsync<SynqLienCaseResponse>(
            HttpMethod.Post, "/api/internal/synqlien/cases", tenantId, actingUserId,
            request, correlationId, idempotencyKey, cancellationToken);

    public Task<SynqLienCallResult<SynqLienLienResponse>> CreateLienAsync(
        Guid tenantId, Guid actingUserId, string idempotencyKey, string correlationId,
        SynqLienLienRequest request, CancellationToken cancellationToken) =>
        SendAsync<SynqLienLienResponse>(
            HttpMethod.Post, "/api/internal/synqlien/liens", tenantId, actingUserId,
            request, correlationId, idempotencyKey, cancellationToken);

    private async Task<SynqLienCallResult<T>> SendAsync<T>(
        HttpMethod method, string path, Guid tenantId, Guid actorUserId, object? body,
        string correlationId, string idempotencyKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString());
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        if (options.OrganizationId != Guid.Empty)
            request.Headers.TryAddWithoutValidation("X-Org-Id", options.OrganizationId.ToString());
        if (tokenIssuer.IsConfigured)
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", tokenIssuer.IssueToken(
                    tenantId.ToString(), actorUserId == Guid.Empty ? null : actorUserId.ToString(),
                    options.ServiceTokenAudience));

        try
        {
            using var response = await httpClient.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                var value = JsonSerializer.Deserialize<T>(raw, JsonOptions);
                return new(true, false, (int)response.StatusCode, value, null, null);
            }

            var retryable = response.StatusCode == HttpStatusCode.RequestTimeout ||
                            response.StatusCode == HttpStatusCode.TooManyRequests ||
                            (int)response.StatusCode >= 500;
            logger.LogWarning("SynqLien destination returned {StatusCode}; body omitted", response.StatusCode);
            return new(false, retryable, (int)response.StatusCode, default,
                retryable ? SynqLienFailureCodes.DestinationUnavailable : SynqLienFailureCodes.DestinationRejected,
                $"SynqLien returned HTTP {(int)response.StatusCode}.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "SynqLien destination request failed; response body omitted");
            return new(false, true, 0, default, SynqLienFailureCodes.DestinationUnavailable,
                "SynqLien destination is unavailable.");
        }
    }
}