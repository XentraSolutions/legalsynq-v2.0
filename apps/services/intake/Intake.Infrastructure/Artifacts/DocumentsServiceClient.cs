using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Context;
using Intake.Application.Artifacts;
using Intake.Application.Classification;
using Intake.Application.Snapshot;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Artifacts;

public sealed class DocumentsServiceClient(
    IHttpClientFactory httpClientFactory,
    IServiceTokenIssuer serviceTokenIssuer,
    ICurrentRequestContext requestContext,
    ILogger<DocumentsServiceClient> logger) : IIntakeDocumentsClient, IIntakeDocumentContentClient
{
    public async Task<DocumentMetadataResult> GetMetadataAsync(
        Guid tenantId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!serviceTokenIssuer.IsConfigured)
            return new(false, documentId, tenantId, string.Empty, string.Empty, null, false);

        try
        {
            var client = httpClientFactory.CreateClient("DocumentsService");
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"internal/intake/documents/{documentId}");
            ApplyAuthorization(request, tenantId);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(false, documentId, tenantId, string.Empty, string.Empty, null, false);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var data = json.RootElement.GetProperty("data");
            return new(
                true,
                documentId,
                data.GetProperty("tenantId").GetGuid(),
                data.GetProperty("status").GetString() ?? string.Empty,
                data.GetProperty("mimeType").GetString() ?? string.Empty,
                data.TryGetProperty("sha256", out var sha) ? sha.GetString() : null,
                data.TryGetProperty("isDeleted", out var deleted) && deleted.GetBoolean());
        }
        catch (HttpRequestException)
        {
            return new(false, documentId, tenantId, string.Empty, string.Empty, null, false);
        }
        catch (JsonException)
        {
            return new(false, documentId, tenantId, string.Empty, string.Empty, null, false);
        }
    }

    public async Task<Stream?> DownloadAsync(
        Guid tenantId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!serviceTokenIssuer.IsConfigured)
            return null;

        try
        {
            var client = httpClientFactory.CreateClient("DocumentsService");
            using var tokenRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"documents/{documentId}/download-url");
            ApplyAuthorization(tokenRequest, tenantId);
            using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
                return null;

            await using var tokenStream = await tokenResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var tokenDocument = await JsonDocument.ParseAsync(
                tokenStream,
                cancellationToken: cancellationToken);
            var redeemUrl = tokenDocument.RootElement
                .GetProperty("data")
                .GetProperty("redeemUrl")
                .GetString();
            if (string.IsNullOrWhiteSpace(redeemUrl))
                return null;

            using var contentRequest = new HttpRequestMessage(HttpMethod.Get, redeemUrl);
            var contentResponse = await client.SendAsync(
                contentRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!contentResponse.IsSuccessStatusCode)
            {
                contentResponse.Dispose();
                return null;
            }
            var contentStream = await contentResponse.Content.ReadAsStreamAsync(cancellationToken);
            return new ResponseOwnedStream(contentStream, contentResponse);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Documents content retrieval failed. Tenant={TenantId} Document={DocumentId}",
                tenantId,
                documentId);
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<DocumentsLookupResult> FindByReferenceAsync(
        Guid tenantId,
        string referenceId,
        string referenceType,
        CancellationToken cancellationToken)
    {
        if (!serviceTokenIssuer.IsConfigured)
        {
            return new(
                false,
                false,
                null,
                null,
                null,
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsServiceUnavailable,
                "The Documents Service authentication is not configured.");
        }

        var path = $"documents?referenceId={Uri.EscapeDataString(referenceId)}&referenceType={Uri.EscapeDataString(referenceType)}&limit=2";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        try
        {
            ApplyAuthorization(request, tenantId);
            var client = httpClientFactory.CreateClient("DocumentsService");
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(
                    false,
                    false,
                    null,
                    null,
                    null,
                    Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsServiceUnavailable,
                    "The Documents Service lookup did not succeed.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (!root.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array ||
                data.GetArrayLength() == 0)
            {
                return new(true, false, null, null, null, null, null);
            }

            var item = data[0];
            if (!item.TryGetProperty("id", out var idProperty) ||
                !Guid.TryParse(idProperty.GetString(), out var documentId))
            {
                return new(
                    false,
                    false,
                    null,
                    null,
                    null,
                    Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsResponseInvalid,
                    "The Documents Service lookup response was invalid.");
            }

            Guid? versionId = null;
            if (item.TryGetProperty("currentVersionId", out var versionProperty) &&
                versionProperty.ValueKind == JsonValueKind.String &&
                Guid.TryParse(versionProperty.GetString(), out var parsedVersionId))
            {
                versionId = parsedVersionId;
            }

            return new(true, true, documentId, versionId, $"documents:{documentId}", null, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(
                false,
                false,
                null,
                null,
                null,
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsServiceUnavailable,
                "The Documents Service lookup timed out.");
        }
        catch (HttpRequestException)
        {
            return new(
                false,
                false,
                null,
                null,
                null,
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsServiceUnavailable,
                "The Documents Service could not be reached.");
        }
        catch (JsonException)
        {
            return new(
                true,
                false,
                null,
                null,
                null,
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsResponseInvalid,
                "The Documents Service lookup response was invalid.");
        }
    }

    private const string DocumentsServiceAudience = "documents-service";

    public async Task<DocumentsUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        long fileSizeBytes,
        Guid tenantId,
        string title,
        string description,
        string productId,
        Guid documentTypeId,
        string referenceId,
        string referenceType,
        CancellationToken cancellationToken)
    {
        if (!serviceTokenIssuer.IsConfigured)
        {
            logger.LogWarning(
                "Documents upload skipped because the Intake service-token issuer is not configured. Tenant={TenantId}",
                tenantId);
            return Failure(
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsServiceUnavailable,
                "The Documents Service authentication is not configured.",
                retryable: false);
        }

        using var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(tenantId.ToString()), "tenantId");
        form.Add(new StringContent(productId), "productId");
        form.Add(new StringContent(referenceId), "referenceId");
        form.Add(new StringContent(referenceType), "referenceType");
        form.Add(new StringContent(documentTypeId.ToString()), "documentTypeId");
        form.Add(new StringContent(title), "title");
        form.Add(new StringContent(description), "description");

        using var request = new HttpRequestMessage(HttpMethod.Post, "documents")
        {
            Content = form,
        };

        try
        {
            ApplyAuthorization(request, tenantId);

            var client = httpClientFactory.CreateClient("DocumentsService");
            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var retryable = (int)response.StatusCode >= 500 ||
                                response.StatusCode == HttpStatusCode.RequestTimeout ||
                                (int)response.StatusCode == 429;
                logger.LogWarning(
                    "Documents upload was rejected. Status={StatusCode} Tenant={TenantId} SizeBytes={SizeBytes}",
                    (int)response.StatusCode,
                    tenantId,
                    fileSizeBytes);
                return Failure(
                    (int)response.StatusCode >= 500
                        ? Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsServiceUnavailable
                        : Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsUploadRejected,
                    $"The Documents Service returned {(int)response.StatusCode}.",
                    retryable);
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.TryGetProperty("data", out var data))
                root = data;

            if (!root.TryGetProperty("id", out var documentIdProperty) ||
                !Guid.TryParse(documentIdProperty.GetString(), out var documentId))
            {
                return Failure(
                    Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsResponseInvalid,
                    "The Documents Service response did not include a valid document id.",
                    retryable: true);
            }

            Guid? versionId = null;
            if (root.TryGetProperty("currentVersionId", out var versionProperty) &&
                versionProperty.ValueKind == JsonValueKind.String &&
                Guid.TryParse(versionProperty.GetString(), out var parsedVersionId))
            {
                versionId = parsedVersionId;
            }

            return new(
                true,
                documentId,
                versionId,
                $"documents:{documentId}",
                null,
                null,
                false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsServiceUnavailable,
                "The Documents Service request timed out.",
                retryable: true);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Documents upload could not reach the service. Tenant={TenantId} SizeBytes={SizeBytes}",
                tenantId,
                fileSizeBytes);
            return Failure(
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsServiceUnavailable,
                "The Documents Service could not be reached.",
                retryable: true);
        }
        catch (JsonException)
        {
            return Failure(
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.DocumentsResponseInvalid,
                "The Documents Service response was invalid.",
                retryable: true);
        }
    }

    private static DocumentsUploadResult Failure(
        string code,
        string message,
        bool retryable) =>
        new(false, null, null, null, code, message, retryable);

    private void ApplyAuthorization(HttpRequestMessage request, Guid tenantId)
    {
        var token = serviceTokenIssuer.IssueToken(
            tenantId.ToString(),
            requestContext.UserId?.ToString(),
            DocumentsServiceAudience);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed class ResponseOwnedStream(Stream inner, HttpResponseMessage response) : Stream
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                response.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            response.Dispose();
            GC.SuppressFinalize(this);
        }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}