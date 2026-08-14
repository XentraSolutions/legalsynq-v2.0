using System.Net.Http.Json;
using System.Text.Json;
using Intake.Application.Matching;
using Intake.Domain.Matching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Intake.Infrastructure.Matching;

public sealed class TenantMatchCandidateOptions
{
    public const string SectionName = "Intake:MatchingCandidates";

    public string? BaseUrl { get; set; }
    public string? InternalToken { get; set; }
    public Dictionary<string, string> Paths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HttpTenantMatchCandidateSource(
    HttpClient httpClient,
    IOptions<TenantMatchCandidateOptions> options,
    ILogger<HttpTenantMatchCandidateSource> logger) : ITenantMatchCandidateSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CandidateProviderResult> SearchAsync(
        Guid tenantId,
        string entityType,
        IReadOnlyList<MatchDiscoveryFact> facts,
        int maxCandidateSearchPool,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return CandidateProviderResult.Failure(
                MatchingFailureCodes.TenantContextInvalid,
                "Tenant context is required for candidate discovery.");

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return CandidateProviderResult.Failure(
                MatchingFailureCodes.EntityProviderUnavailable,
                $"No candidate source is configured for {entityType}.");
        }

        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return CandidateProviderResult.Failure(
                MatchingFailureCodes.EntityProviderUnavailable,
                "The configured candidate source URL is invalid.");
        }

        var path = settings.Paths.TryGetValue(entityType, out var configuredPath)
            ? configuredPath
            : $"/internal/matching/candidates/{entityType}";
        if (!Uri.TryCreate(path, UriKind.Relative, out _))
        {
            return CandidateProviderResult.Failure(
                MatchingFailureCodes.EntityProviderUnavailable,
                "The configured candidate source path must be relative.");
        }
        var request = new
        {
            entityType,
            maxCandidateSearchPool,
            facts,
        };

        try
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(baseUri, path))
            {
                Content = JsonContent.Create(request, options: JsonOptions),
            };
            message.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString());
            if (string.IsNullOrWhiteSpace(settings.InternalToken))
            {
                return CandidateProviderResult.Failure(
                    MatchingFailureCodes.EntityProviderUnavailable,
                    "Candidate source authentication is not configured.");
            }
            message.Headers.TryAddWithoutValidation("X-Internal-Token", settings.InternalToken);
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return CandidateProviderResult.Failure(
                    MatchingFailureCodes.CandidateSearchFailed,
                    $"Candidate source returned HTTP {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<CandidateSearchResponse>(
                JsonOptions,
                cancellationToken);
            if (payload?.Items is null || payload.TenantId != tenantId ||
                payload.Items.Any(item => item.TenantId != tenantId))
            {
                return CandidateProviderResult.Failure(
                    MatchingFailureCodes.CandidateSearchFailed,
                    "Candidate source returned a projection outside the requested tenant.");
            }

            return CandidateProviderResult.Success(
                payload.Items
                    .Where(item => item.EntityId != Guid.Empty)
                    .Take(maxCandidateSearchPool)
                    .Select(item => new TenantMatchCandidate(
                        item.EntityId,
                        item.DisplayLabel ?? string.Empty,
                        item.Fields?.ToDictionary(
                            field => field.Key,
                            field => new TenantMatchCandidateField(
                                field.Value.Value,
                                field.Value.ComparisonKey,
                                field.Value.DataType),
                            StringComparer.OrdinalIgnoreCase)
                        ?? new Dictionary<string, TenantMatchCandidateField>(
                            StringComparer.OrdinalIgnoreCase)))
                    .ToArray());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CandidateProviderResult.Failure(
                Domain.Matching.MatchingFailureCodes.CandidateSearchFailed,
                "Candidate source timed out.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                "Tenant match candidate source failed. EntityType={EntityType} Status={Status}",
                entityType,
                ex.StatusCode);
            return CandidateProviderResult.Failure(
                MatchingFailureCodes.EntityProviderUnavailable,
                "Candidate source was unavailable.");
        }
        catch (JsonException)
        {
            return CandidateProviderResult.Failure(
                MatchingFailureCodes.CandidateSearchFailed,
                "Candidate source returned malformed JSON.");
        }
    }

    private sealed record CandidateSearchResponse(
        Guid TenantId,
        IReadOnlyList<CandidateItem>? Items);

    private sealed record CandidateItem(
        Guid TenantId,
        Guid EntityId,
        string? DisplayLabel,
        IReadOnlyDictionary<string, CandidateFieldItem>? Fields);

    private sealed record CandidateFieldItem(
        string? Value,
        string? ComparisonKey,
        string? DataType);
}