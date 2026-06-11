using System.Text.Json;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Context;
using Comms.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Comms.Infrastructure.Documents;

public sealed class DocumentServiceClient : IDocumentServiceClient
{
    private const string DocumentsServiceAudience = "documents-service";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceTokenIssuer _serviceTokenIssuer;
    private readonly ICurrentRequestContext _requestContext;
    private readonly ILogger<DocumentServiceClient> _logger;

    public DocumentServiceClient(
        IHttpClientFactory httpClientFactory,
        IServiceTokenIssuer serviceTokenIssuer,
        ICurrentRequestContext requestContext,
        ILogger<DocumentServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceTokenIssuer = serviceTokenIssuer;
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task<DocumentValidationResult> ValidateDocumentAsync(
        Guid documentId, Guid expectedTenantId, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentsService");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/documents/{documentId}");
            ApplyDocumentsAuthorization(request, expectedTenantId);
            var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Documents service returned {StatusCode} for document {DocumentId}",
                    response.StatusCode, documentId);
                return new DocumentValidationResult(false, null);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            Guid? docTenantId = null;

            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var dataElement))
                root = dataElement;

            if (root.TryGetProperty("tenantId", out var tenantProp))
            {
                var tenantStr = tenantProp.ValueKind == JsonValueKind.String
                    ? tenantProp.GetString()
                    : tenantProp.ToString();

                if (Guid.TryParse(tenantStr, out var parsedTenantId))
                    docTenantId = parsedTenantId;
            }

            if (!docTenantId.HasValue)
            {
                _logger.LogWarning(
                    "Document {DocumentId} response missing tenantId — cannot verify tenant ownership",
                    documentId);
                return new DocumentValidationResult(true, null);
            }

            return new DocumentValidationResult(true, docTenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to validate document {DocumentId} with Documents service", documentId);
            return new DocumentValidationResult(false, null);
        }
    }

    private void ApplyDocumentsAuthorization(HttpRequestMessage request, Guid tenantId)
    {
        if (!_serviceTokenIssuer.IsConfigured)
            return;

        try
        {
            var token = _serviceTokenIssuer.IssueToken(
                tenantId.ToString(),
                _requestContext.UserId?.ToString(),
                DocumentsServiceAudience);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to mint Documents service token for tenant {TenantId}", tenantId);
        }
    }
}
