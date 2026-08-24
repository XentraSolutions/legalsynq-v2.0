using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Intake.Application.Artifacts;
using Intake.Application.Classification;
using Intake.Application.Configuration;
using Intake.Application.Extraction;
using Intake.Contracts.Configuration;
using Intake.Contracts.Normalization;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Normalization;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Normalization;

public sealed class NormalizationService(
    IArtifactNormalizationRepository repository,
    IArtifactExtractionRepository extractionRepository,
    IClassificationRepository classificationRepository,
    IIntakeConfigurationService configurationService,
    IFactNormalizerRegistry normalizerRegistry,
    INormalizationAuditSink auditSink,
    ILogger<NormalizationService> logger) : IArtifactNormalizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<NormalizationProfileResponse>> ListProfilesAsync(
        CancellationToken cancellationToken)
    {
        var profiles = await repository.ListProfilesAsync(cancellationToken);
        return profiles
            .OrderBy(profile => profile.Code)
            .ThenByDescending(profile => profile.Version)
            .Select(MapProfile)
            .ToArray();
    }

    public async Task<ArtifactNormalizationResponse?> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var extraction = await FindCurrentExtractionAsync(
            tenantId,
            artifactId,
            cancellationToken);
        if (extraction is null)
            return null;

        var normalization = await repository.FindCurrentAsync(
            tenantId,
            artifactId,
            extraction.Id,
            cancellationToken);
        return normalization is null ? null : Map(normalization);
    }

    public async Task<IReadOnlyList<ArtifactNormalizationResponse>> GetHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        (await repository.ListHistoryAsync(tenantId, artifactId, cancellationToken))
        .Select(Map)
        .ToArray();

    public async Task<ArtifactNormalizationResponse> NormalizeAsync(
        Guid tenantId,
        Guid artifactId,
        string? processingProfileCode,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var artifact = await classificationRepository.FindArtifactAsync(
                tenantId,
                artifactId,
                cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "ARTIFACT_NOT_FOUND",
                "The Intake artifact was not found for the current tenant.");

        var resolved = await configurationService.ResolveAsync(
            tenantId,
            processingProfileCode,
            cancellationToken);
        var configuration = resolved.EffectiveConfiguration;
        if (!configuration.EnableNormalization)
            throw IntakeConfigurationException.BadRequest(
                NormalizationFailureCodes.NormalizationDisabled,
                "Normalization is disabled by the tenant processing profile.");

        var profileCode = configuration.NormalizationProfileCode.Trim().ToUpperInvariant();
        var profile = await repository.FindProfileAsync(
                profileCode,
                null,
                cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                NormalizationFailureCodes.ProfileUnavailable,
                $"Normalization profile '{profileCode}' is not available.");

        var extraction = await FindCurrentExtractionAsync(
            tenantId,
            artifactId,
            cancellationToken);
        if (extraction is null ||
            !string.Equals(extraction.Status, ExtractionStatuses.Completed, StringComparison.Ordinal))
            throw IntakeConfigurationException.BadRequest(
                NormalizationFailureCodes.ExtractionRequired,
                "Normalization requires a successfully completed current B08 extraction.");

        if (extraction.TenantId != tenantId ||
            extraction.IntakeArtifactId != artifactId ||
            extraction.Facts.Any(fact => fact.TenantId != tenantId))
            throw IntakeConfigurationException.Forbidden(
                NormalizationFailureCodes.ExtractionNotCurrent,
                "The extraction and source facts are not owned by the current tenant.");

        var executionKey = BuildExecutionKey(
            artifact,
            extraction,
            profile,
            configuration);
        var existing = await repository.FindByExecutionKeyAsync(
            tenantId,
            executionKey,
            cancellationToken);
        if (existing is { Status: NormalizationRunStatuses.Processing })
            throw IntakeConfigurationException.Conflict(
                NormalizationFailureCodes.ConcurrencyConflict,
                "Another normalization attempt is already processing this execution.");
        if (existing is not null)
            return Map(existing);

        var now = DateTimeOffset.UtcNow;
        var normalization = new ArtifactNormalization
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            IntakeArtifactId = artifactId,
            ArtifactExtractionId = extraction.Id,
            NormalizationProfileCode = profile.Code,
            NormalizationProfileVersion = profile.Version,
            NormalizationVersion = profile.NormalizerVersion,
            ExecutionKey = executionKey,
            Status = NormalizationRunStatuses.Processing,
            RequestedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        if (!await repository.TryAddNormalizationAsync(normalization, cancellationToken))
        {
            var concurrent = await repository.FindByExecutionKeyAsync(
                tenantId,
                executionKey,
                cancellationToken);
            if (concurrent is null)
                throw IntakeConfigurationException.Conflict(
                    NormalizationFailureCodes.ConcurrencyConflict,
                    "Another normalization attempt changed while this request was starting.");
            if (concurrent.Status == NormalizationRunStatuses.Processing)
                throw IntakeConfigurationException.Conflict(
                    NormalizationFailureCodes.ConcurrencyConflict,
                    "Another normalization attempt is already processing this execution.");
            return Map(concurrent);
        }

        try
        {
            var options = BuildOptions(configuration, profile);
            var allowedFactCodes = ParseFactCodes(profile.SupportedFactCodesJson);
            var outcomes = extraction.Facts
                .OrderBy(fact => fact.FactOrdinal)
                .Select(fact => NormalizeFact(fact, allowedFactCodes, options))
                .ToList();
            ApplyCrossFactValidation(outcomes);

            var normalizedFacts = outcomes
                .Select(outcome => new ArtifactNormalizedFact
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = tenantId,
                    ArtifactNormalizationId = normalization.Id,
                    ArtifactExtractedFactId = outcome.Source.Id,
                    FactCode = outcome.Source.FactCode,
                    DataType = outcome.Source.DataType,
                    RawValue = outcome.Source.RawValue,
                    NormalizedValue = outcome.Result.NormalizedValue,
                    NormalizedJson = outcome.Result.NormalizedJson,
                    ComparisonKey = outcome.Result.ComparisonKey,
                    NormalizationStatus = outcome.Result.NormalizationStatus,
                    ValidationStatus = outcome.Result.ValidationStatus,
                    NormalizationMethod = outcome.Result.NormalizationMethod,
                    NormalizationVersion = outcome.Result.NormalizationVersion,
                    SourceConfidence = outcome.Source.Confidence,
                    WarningCodesJson = JsonSerializer.Serialize(
                        outcome.Result.WarningCodes,
                        JsonOptions),
                    EvidenceReferenceJson = outcome.Source.EvidenceJson ?? "[]",
                    Ordinal = outcome.Source.FactOrdinal,
                    CreatedAt = now,
                    UpdatedAt = now,
                })
                .ToArray();

            normalization.Status = DetermineAggregateStatus(normalizedFacts);
            normalization.IsCurrent = true;
            normalization.CurrentResultMarker = "CURRENT";
            normalization.CompletedAt = DateTimeOffset.UtcNow;
            normalization.UpdatedAt = normalization.CompletedAt.Value;
            await repository.FinalizeCurrentAsync(
                tenantId,
                artifactId,
                normalization,
                normalizedFacts,
                cancellationToken);

            await RecordAuditAsync(
                tenantId,
                artifactId,
                normalization,
                normalizedFacts,
                "normalization.completed",
                correlationId,
                actorId,
                cancellationToken);
            return Map(normalization);
        }
        catch (Exception exception) when (exception is not IntakeConfigurationException)
        {
            logger.LogError(
                exception,
                "Deterministic normalization failed. Tenant={TenantId} Artifact={ArtifactId} Extraction={ArtifactExtractionId} Normalization={NormalizationId}",
                tenantId,
                artifactId,
                extraction.Id,
                normalization.Id);
            normalization.Status = NormalizationRunStatuses.Failed;
            normalization.FailureCode = NormalizationFailureCodes.ExecutionFailed;
            normalization.FailureMessage = "The normalization execution could not be completed.";
            normalization.IsCurrent = false;
            normalization.CurrentResultMarker = null;
            normalization.UpdatedAt = DateTimeOffset.UtcNow;
            await repository.SaveAsync(cancellationToken);
            await RecordAuditAsync(
                tenantId,
                artifactId,
                normalization,
                [],
                "normalization.failed",
                correlationId,
                actorId,
                cancellationToken);
            return Map(normalization);
        }
    }

    private async Task<ArtifactExtraction?> FindCurrentExtractionAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var classification = await classificationRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            cancellationToken);
        return classification is null
            ? null
            : await extractionRepository.FindCurrentAsync(
                tenantId,
                artifactId,
                classification.Id,
                cancellationToken);
    }

    private static FactNormalizationOptions BuildOptions(
        LienIntakeV1Configuration configuration,
        NormalizationProfileDefinition profile) =>
        new(
            Normalize(configuration.DefaultCountryCode, profile.DefaultCountryCode),
            Normalize(configuration.DefaultCurrencyCode, profile.DefaultCurrencyCode),
            CultureInfo.GetCultureInfo(
                string.IsNullOrWhiteSpace(configuration.DateCulture)
                    ? profile.DefaultDateCulture
                    : configuration.DateCulture),
            configuration.AllowAmbiguousDateNormalization,
            profile.UnicodeForm,
            profile.NormalizerVersion);

    private (ArtifactExtractedFact Source, FactNormalizationResult Result) NormalizeFact(
        ArtifactExtractedFact source,
        IReadOnlySet<string> allowedFactCodes,
        FactNormalizationOptions options)
    {
        if (!allowedFactCodes.Contains(source.FactCode) ||
            !normalizerRegistry.TryResolve(source.FactCode, source.DataType, out var normalizer))
        {
            return (
                source,
                new FactNormalizationResult(
                    null,
                    null,
                    null,
                    NormalizationStatuses.Unsupported,
                    ValidationStatuses.Unverified,
                    ["NORMALIZER_UNAVAILABLE"],
                    "NONE",
                    options.NormalizationVersion));
        }

        return (source, normalizer.Normalize(new FactNormalizationInput(
            source.FactCode,
            source.DataType,
            source.RawValue,
            options)));
    }

    private static void ApplyCrossFactValidation(
        IList<(ArtifactExtractedFact Source, FactNormalizationResult Result)> outcomes)
    {
        ApplyDateRangeWarning(outcomes, "DATE_OF_SERVICE_START", "DATE_OF_SERVICE_END");
        ApplyDateRangeWarning(outcomes, "EFFECTIVE_DATE", "EXPIRATION_DATE");
        ApplyDateRangeWarning(outcomes, "DATE_OF_BIRTH", "DOCUMENT_DATE");
    }

    private static void ApplyDateRangeWarning(
        IList<(ArtifactExtractedFact Source, FactNormalizationResult Result)> outcomes,
        string fromCode,
        string toCode)
    {
        var from = outcomes.FirstOrDefault(item =>
            item.Source.FactCode == fromCode &&
            item.Result.ParsedDate.HasValue);
        var to = outcomes.FirstOrDefault(item =>
            item.Source.FactCode == toCode &&
            item.Result.ParsedDate.HasValue);
        if (from.Source is null || to.Source is null ||
            from.Result.ParsedDate <= to.Result.ParsedDate)
            return;

        for (var index = 0; index < outcomes.Count; index++)
        {
            if (outcomes[index].Source.Id != from.Source.Id &&
                outcomes[index].Source.Id != to.Source.Id)
                continue;
            var result = outcomes[index].Result;
            outcomes[index] = (
                outcomes[index].Source,
                result with
                {
                    ValidationStatus = ValidationStatuses.InvalidFormat,
                    WarningCodes = result.WarningCodes
                        .Append(NormalizationWarningCodes.DateRangeInvalid)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                });
        }
    }

    private static string DetermineAggregateStatus(
        IReadOnlyList<ArtifactNormalizedFact> facts)
    {
        if (facts.Count == 0)
            return NormalizationRunStatuses.Completed;
        return facts.Any(fact =>
                fact.NormalizationStatus is NormalizationStatuses.Partial
                    or NormalizationStatuses.Invalid
                    or NormalizationStatuses.Ambiguous
                    or NormalizationStatuses.Unsupported)
            ? NormalizationRunStatuses.Partial
            : NormalizationRunStatuses.Completed;
    }

    private async Task RecordAuditAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactNormalization normalization,
        IReadOnlyList<ArtifactNormalizedFact> facts,
        string action,
        string? correlationId,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        await auditSink.RecordAsync(
            new NormalizationAuditEntry(
                tenantId,
                artifactId,
                normalization.ArtifactExtractionId,
                normalization.Id,
                action,
                normalization.Status,
                normalization.NormalizationProfileCode,
                facts.Count,
                facts.Count(fact => fact.NormalizationStatus == NormalizationStatuses.Normalized),
                facts.Count(fact => fact.NormalizationStatus == NormalizationStatuses.Invalid),
                facts.Count(fact => fact.NormalizationStatus == NormalizationStatuses.Ambiguous),
                correlationId,
                actorId),
            cancellationToken);
    }

    private static IReadOnlySet<string> ParseFactCodes(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.EnumerateArray()
                .Select(item => item.TryGetProperty("code", out var code)
                    ? code.GetString()
                    : null)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet(StringComparer.Ordinal)!;
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static string BuildExecutionKey(
        IntakeArtifact artifact,
        ArtifactExtraction extraction,
        NormalizationProfileDefinition profile,
        LienIntakeV1Configuration configuration)
    {
        var input = string.Join(
            "|",
            artifact.TenantId,
            artifact.Id,
            artifact.Sha256,
            extraction.Id,
            extraction.ExecutionKey,
            extraction.AttemptNumber,
            profile.Code,
            profile.Version,
            profile.NormalizerVersion,
            configuration.DefaultCountryCode,
            configuration.DefaultCurrencyCode,
            configuration.DateCulture,
            configuration.AllowAmbiguousDateNormalization);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))
            .ToLowerInvariant();
    }

    private static NormalizationProfileResponse MapProfile(
        NormalizationProfileDefinition profile) =>
        new(
            profile.Code,
            profile.DisplayName,
            profile.Description,
            profile.Version,
            profile.IsActive,
            profile.IsSystemDefined,
            profile.NormalizerVersion,
            profile.UnicodeForm,
            profile.ComparisonKeyStrategy,
            profile.DefaultDateCulture,
            profile.DefaultCountryCode,
            profile.DefaultCurrencyCode);

    private static ArtifactNormalizationResponse Map(
        ArtifactNormalization normalization) =>
        new(
            normalization.Id,
            normalization.IntakeArtifactId,
            normalization.ArtifactExtractionId,
            normalization.NormalizationProfileCode,
            normalization.NormalizationProfileVersion,
            normalization.NormalizationVersion,
            normalization.Status,
            normalization.IsCurrent,
            normalization.FailureCode,
            normalization.FailureMessage,
            normalization.RequestedAt,
            normalization.CompletedAt,
            normalization.Facts
                .OrderBy(fact => fact.Ordinal)
                .Select(fact => new NormalizedFactResponse(
                    fact.Id,
                    fact.ArtifactExtractedFactId,
                    fact.FactCode,
                    fact.DataType,
                    fact.RawValue,
                    fact.NormalizedValue,
                    fact.NormalizedJson,
                    fact.ComparisonKey,
                    fact.NormalizationStatus,
                    fact.ValidationStatus,
                    ParseStringArray(fact.WarningCodesJson),
                    fact.SourceConfidence,
                    ParseStringArray(fact.EvidenceReferenceJson),
                    fact.Ordinal))
                .ToArray());

    private static IReadOnlyList<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
}