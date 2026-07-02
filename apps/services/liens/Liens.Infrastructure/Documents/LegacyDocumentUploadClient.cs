using System.Net.Http.Headers;
using System.Text.Json;
using BuildingBlocks.Authentication.ServiceTokens;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Documents;

public sealed class LegacyDocumentUploadClient : ILegacyDocumentUploadClient
{
    private const string DocumentsServiceAudience = "documents-service";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceTokenIssuer _serviceTokenIssuer;
    private readonly ILogger<LegacyDocumentUploadClient> _logger;

    public LegacyDocumentUploadClient(
        IHttpClientFactory httpClientFactory,
        IServiceTokenIssuer serviceTokenIssuer,
        ILogger<LegacyDocumentUploadClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceTokenIssuer = serviceTokenIssuer;
        _logger = logger;
    }

    public async Task<LegacyDocumentUploadResult> UploadAsync(
        LegacyDocumentUploadRequest request,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(request.TenantId.ToString()), "tenantId");
        content.Add(new StringContent(request.DocumentTypeId.ToString()), "documentTypeId");
        content.Add(new StringContent("SYNQ_LIENS"), "productId");
        content.Add(new StringContent(request.ReferenceId.ToString()), "referenceId");
        content.Add(new StringContent(request.ReferenceType), "referenceType");
        content.Add(new StringContent(request.Title), "title");

        if (!string.IsNullOrWhiteSpace(request.Description))
            content.Add(new StringContent(request.Description), "description");

        using var fileContent = new StreamContent(request.Content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(request.ContentType)
                ? "application/octet-stream"
                : request.ContentType);
        content.Add(fileContent, "file", request.FileName);

        var client = _httpClientFactory.CreateClient("DocumentsService");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/documents")
        {
            Content = content,
        };
        ApplyDocumentsAuthorization(httpRequest, request.TenantId, request.ActingUserId);

        using var response = await client.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Legacy liens document upload failed with {StatusCode}: {Body}",
                response.StatusCode,
                body);
            throw new InvalidOperationException("Document upload failed.");
        }

        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("data", out var data))
            throw new InvalidOperationException("Document upload returned an unexpected response.");

        Guid? documentId = null;
        if (data.TryGetProperty("id", out var idProp) &&
            Guid.TryParse(idProp.GetString(), out var parsedDocumentId))
        {
            documentId = parsedDocumentId;
        }

        var url = documentId.HasValue
            ? $"/documents/{documentId.Value}"
            : string.Empty;

        if (data.TryGetProperty("url", out var urlProp))
            url = urlProp.GetString() ?? url;
        else if (data.TryGetProperty("downloadUrl", out var downloadUrlProp))
            url = downloadUrlProp.GetString() ?? url;
        else if (data.TryGetProperty("redeemUrl", out var redeemUrlProp))
            url = redeemUrlProp.GetString() ?? url;

        return new LegacyDocumentUploadResult
        {
            DocumentId = documentId,
            Url = url,
        };
    }

    private void ApplyDocumentsAuthorization(HttpRequestMessage request, Guid tenantId, Guid actorUserId)
    {
        if (!_serviceTokenIssuer.IsConfigured)
            return;

        try
        {
            var token = _serviceTokenIssuer.IssueToken(
                tenantId.ToString(),
                actorUserId.ToString(),
                DocumentsServiceAudience);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to mint Documents service token for tenant {TenantId}", tenantId);
        }
    }
}
