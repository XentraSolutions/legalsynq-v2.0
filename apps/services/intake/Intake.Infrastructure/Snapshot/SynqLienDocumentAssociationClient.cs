using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Authentication.ServiceTokens;
using Intake.Application.Snapshot;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Snapshot;

public sealed class SynqLienDocumentAssociationClient(
    HttpClient httpClient,
    IServiceTokenIssuer tokenIssuer,
    SynqLienDestinationOptions options,
    ILogger<SynqLienDocumentAssociationClient> logger)
    : IDocumentAssociationDestinationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DocumentAssociationCallResult> AssociateAsync(
        Guid tenantId,
        Guid actingUserId,
        string idempotencyKey,
        string correlationId,
        string targetType,
        Guid targetId,
        Guid? relatedCaseId,
        Guid documentId,
        string documentRole,
        string documentReference,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/internal/synqlien/document-associations")
        {
            Content = JsonContent.Create(new
            {
                documentId,
                documentRole,
                documentReference,
                targetType,
                targetId,
                relatedCaseId,
            }, options: JsonOptions),
        };
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString());
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        if (tokenIssuer.IsConfigured)
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                tokenIssuer.IssueToken(
                    tenantId.ToString(),
                    actingUserId == Guid.Empty ? null : actingUserId.ToString(),
                    options.ServiceTokenAudience));

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                using var json = JsonDocument.Parse(raw);
                var data = json.RootElement.TryGetProperty("data", out var value)
                    ? value
                    : json.RootElement;
                var reference = data.TryGetProperty("associationId", out var id)
                    ? id.GetString()
                    : null;
                return new(true, false, (int)response.StatusCode, reference, null, null);
            }

            var retryable = response.StatusCode == HttpStatusCode.RequestTimeout
                || response.StatusCode == HttpStatusCode.TooManyRequests
                || (int)response.StatusCode >= 500;
            logger.LogWarning(
                "SynqLien document association returned status {StatusCode}; body omitted",
                (int)response.StatusCode);
            return new(
                false,
                retryable,
                (int)response.StatusCode,
                response.StatusCode == HttpStatusCode.Conflict ? idempotencyKey : null,
                retryable ? "DESTINATION_UNAVAILABLE" : "DESTINATION_REJECTED",
                $"SynqLien returned HTTP {(int)response.StatusCode}.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "SynqLien document association request failed");
            return new(false, true, 0, null, "DESTINATION_UNAVAILABLE",
                "SynqLien document association destination is unavailable.");
        }
    }
}