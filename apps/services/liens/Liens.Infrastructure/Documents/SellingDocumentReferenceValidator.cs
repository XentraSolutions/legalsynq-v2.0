using System.Net.Http.Headers;
using System.Text.Json;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Exceptions;
using Liens.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Documents;

public sealed class SellingDocumentReferenceValidator : ISellingDocumentReferenceValidator
{
    private const string DocumentsServiceAudience = "documents-service";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceTokenIssuer _serviceTokenIssuer;
    private readonly ILogger<SellingDocumentReferenceValidator> _logger;

    public SellingDocumentReferenceValidator(
        IHttpClientFactory httpClientFactory,
        IServiceTokenIssuer serviceTokenIssuer,
        ILogger<SellingDocumentReferenceValidator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceTokenIssuer = serviceTokenIssuer;
        _logger = logger;
    }

    public async Task<bool> IsAccessibleAsync(
        Guid tenantId,
        Guid sellerOrgId,
        Guid actingUserId,
        Guid lienId,
        Guid? caseId,
        Guid documentId,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/documents/{documentId}");
        ApplyAuthorization(request, tenantId, actingUserId);
        request.Headers.TryAddWithoutValidation("X-Organization-Id", sellerOrgId.ToString());

        try
        {
            var client = _httpClientFactory.CreateClient("DocumentsService");
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return false;

            using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            var document = body.RootElement.TryGetProperty("data", out var data) ? data : body.RootElement;
            if (!TryGetGuid(document, "tenantId", out var documentTenantId) || documentTenantId != tenantId)
                return false;
            if (!document.TryGetProperty("productId", out var product) ||
                !string.Equals(product.GetString(), "SYNQ_LIENS", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!document.TryGetProperty("referenceId", out var reference) ||
                !Guid.TryParse(reference.GetString(), out var referenceId))
                return false;

            // Documents has no independent organization column. Its reference
            // is therefore constrained to the seller-owned lien or case already
            // authorized by this API request.
            return referenceId == lienId || (caseId.HasValue && referenceId == caseId.Value);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Documents validation transport failed for document {DocumentId}", documentId);
            throw new ServiceUnavailableException(
                "The document was uploaded, but the document service could not verify it for this lien. Please try again.");
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Documents validation timed out for document {DocumentId}", documentId);
            throw new ServiceUnavailableException(
                "The document was uploaded, but verification timed out before it could be attached to this lien. Please try again.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Documents validation returned invalid metadata for document {DocumentId}", documentId);
            throw new ServiceUnavailableException(
                "The document was uploaded, but the document service returned an invalid verification response. Please try again.");
        }
    }

    private void ApplyAuthorization(HttpRequestMessage request, Guid tenantId, Guid actingUserId)
    {
        if (!_serviceTokenIssuer.IsConfigured)
            return;

        try
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _serviceTokenIssuer.IssueToken(tenantId.ToString(), actingUserId.ToString(), DocumentsServiceAudience));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to mint Documents service token for tenant {TenantId}", tenantId);
        }
    }

    private static bool TryGetGuid(JsonElement element, string name, out Guid value)
    {
        value = Guid.Empty;
        return element.TryGetProperty(name, out var property) && Guid.TryParse(property.GetString(), out value);
    }
}
