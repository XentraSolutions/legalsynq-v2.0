using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Intake.Application.Artifacts;
using Intake.Application.Classification;
using Intake.Application.Configuration;
using Intake.Application.Extraction;
using Intake.Application.Normalization;
using Intake.Contracts.Configuration;
using Intake.Contracts.Matching;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Matching;
using Intake.Domain.Normalization;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Matching;

public sealed class MatchingService(
    IArtifactMatchingRepository repository,
    IArtifactNormalizationRepository normalizationRepository,
    IArtifactExtractionRepository extractionRepository,
    IClassificationRepository classificationRepository,
    IIntakeArtifactRepository artifactRepository,
    IIntakeConfigurationService configurationService,
    IMatchCandidateProviderRegistry providerRegistry,
    IMatchingAuditSink auditSink,
    ILogger<MatchingService> logger) : IArtifactMatchingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<MatchingProfileResponse>> ListProfilesAsync(
        CancellationToken cancellationToken) =>
        (await repository.ListProfilesAsync(cancellationToken))
        .Select(profile =>
        {
            var document = MatchingProfileParser.Parse(profile);
            return new MatchingProfileResponse(
                profile.Code,
                profile.DisplayName,
                profile.Description,
                profile.Version,
                profile.ScoringVersion,
                profile.IsActive,
                document.EntityTypes);
        })
        .ToArray();

    public async Task<ArtifactMatchResponse?> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var context = await LoadCurrentNormalizationAsync(
            tenantId,
            artifactId,
            cancellationToken);
        if (context is null)
            return null;

        var run = await repository.FindCurrentAsync(
            tenantId,
            artifactId,
            context.Normalization.Id,
            cancellationToken);
        return run is null ? null : Map(run);
    }

    public async Task<IReadOnlyList<ArtifactMatchResponse>> GetHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        (await repository.ListHistoryAsync(tenantId, artifactId, cancellationToken))
        .Select(Map)
        .ToArray();

    public async Task<ArtifactMatchResponse> MatchAsync(
        Guid tenantId,
        Guid artifactId,
        string? processingProfileCode,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var context = await LoadCurrentNormalizationAsync(
            tenantId,
            artifactId,
            cancellationToken)
            ?? throw IntakeConfigurationException.BadRequest(
                MatchingFailureCodes.NormalizationRequired,
                "A completed or partial current B09 normalization is required before matching.");
        var artifact = context.Artifact;

        var resolved = await configurationService.ResolveAsync(
            tenantId,
            processingProfileCode,
            cancellationToken);
        var configuration = resolved.EffectiveConfiguration;
        if (!configuration.EnableMatching)
            throw IntakeConfigurationException.BadRequest(
                MatchingFailureCodes.MatchingDisabled,
                "Matching is disabled by the tenant processing profile.");

        var profileCode = configuration.MatchingProfileCode.Trim().ToUpperInvariant();
        var profile = await repository.FindProfileAsync(
                profileCode,
                null,
                cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                MatchingFailureCodes.ProfileUnavailable,
                "The configured matching profile is unavailable or inactive.");
        var profileDocument = MatchingProfileParser.Parse(profile);

        var sourceFacts = context.Normalization.Facts
            .Where(fact => IsUsable(fact.ValidationStatus) &&
                           fact.NormalizationStatus is
                               NormalizationStatuses.Normalized or
                               NormalizationStatuses.Partial &&
                           (!string.IsNullOrWhiteSpace(fact.ComparisonKey) ||
                            !string.IsNullOrWhiteSpace(fact.NormalizedValue)))
            .OrderBy(fact => fact.Ordinal)
            .ThenBy(fact => fact.Id)
            .Select(fact => new MatchDiscoveryFact(
                fact.Id,
                fact.FactCode,
                fact.NormalizedValue,
                fact.ComparisonKey,
                fact.ValidationStatus,
                fact.SourceConfidence))
            .ToArray();
        if (sourceFacts.Length == 0)
            throw IntakeConfigurationException.BadRequest(
                MatchingFailureCodes.NoUsableFacts,
                "The current normalization contains no usable facts for matching.");

        var executionKey = BuildExecutionKey(
            context.Normalization,
            profile,
            profileDocument);
        var run = new ArtifactMatchRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IntakeArtifactId = artifactId,
            ArtifactNormalizationId = context.Normalization.Id,
            MatchingProfileCode = profile.Code,
            MatchingProfileVersion = profile.Version,
            ScoringVersion = profile.ScoringVersion,
            ExecutionKey = executionKey,
            Status = MatchRunStatuses.Processing,
            RequestedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        if (!await repository.TryAddMatchRunAsync(run, cancellationToken))
        {
            var existing = await repository.FindByExecutionKeyAsync(
                tenantId,
                executionKey,
                cancellationToken);
            if (existing is not null)
                return Map(existing);
            throw IntakeConfigurationException.Conflict(
                MatchingFailureCodes.CandidateSearchFailed,
                "A matching run with the same execution identity is already being created.");
        }

        await RecordAuditAsync(
            "MATCHING_REQUESTED",
            run,
            [],
            0,
            0,
            actorId,
            correlationId,
            cancellationToken);

        try
        {
            var entityMatches = new List<ArtifactEntityMatch>();
            var fields = new List<ArtifactMatchField>();
            var providerFailures = new List<string>();
            var processedEntityTypes = new List<string>();
            var successfulProviders = 0;
            var candidateCount = 0;
            var now = DateTimeOffset.UtcNow;

            foreach (var entityType in profileDocument.EntityTypes
                         .Where(MatchingEntityTypes.Supported.Contains)
                         .Distinct(StringComparer.Ordinal))
            {
                if (!profileDocument.EntityRules.TryGetValue(entityType, out var entityRule))
                    continue;

                var relevantFacts = sourceFacts
                    .Where(fact => entityRule.Fields.Any(field =>
                        MatchScoring.FactCodeMatches(field.FactCode, fact.FactCode)))
                    .ToArray();
                if (relevantFacts.Length == 0)
                    continue;

                processedEntityTypes.Add(entityType);
                var provider = providerRegistry.Find(entityType);
                if (provider is null)
                {
                    providerFailures.Add($"{entityType}:{MatchingFailureCodes.EntityProviderUnavailable}");
                    continue;
                }

                CandidateProviderResult providerResult;
                try
                {
                    providerResult = await provider.SearchAsync(
                        tenantId,
                        relevantFacts,
                        configuration.MaxCandidateSearchPool,
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(
                        ex,
                        "Tenant match candidate provider failed. Tenant={TenantId} EntityType={EntityType}",
                        tenantId,
                        entityType);
                    providerFailures.Add($"{entityType}:{MatchingFailureCodes.CandidateSearchFailed}");
                    continue;
                }

                if (!providerResult.Succeeded)
                {
                    providerFailures.Add(
                        $"{entityType}:{providerResult.FailureCode ?? MatchingFailureCodes.CandidateSearchFailed}");
                    continue;
                }

                successfulProviders++;
                var scored = providerResult.Candidates
                    .Take(configuration.MaxCandidateSearchPool)
                    .Select(candidate => MatchScoring.Score(
                        entityType,
                        entityRule,
                        candidate,
                        relevantFacts,
                        configuration.UseSourceConfidenceInScoring))
                    .Where(result => result.Score >= (decimal)configuration.MinimumCandidateScore)
                    .OrderByDescending(result => result.Score)
                    .ThenByDescending(result => result.MatchedFieldCount)
                    .ThenBy(result => result.ConflictingFieldCount)
                    .ThenBy(result => result.Candidate.EntityId)
                    .Take(configuration.MaxCandidatesPerEntityType)
                    .ToArray();

                candidateCount += scored.Length;
                for (var index = 0; index < scored.Length; index++)
                {
                    var result = scored[index];
                    var entityMatch = new ArtifactEntityMatch
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ArtifactMatchRunId = run.Id,
                        EntityType = entityType,
                        CandidateEntityId = result.Candidate.EntityId,
                        CandidateDisplayLabel = result.Candidate.DisplayLabel,
                        Score = result.Score,
                        Rank = index + 1,
                        MatchStatus = result.Status,
                        IsTopCandidate = index == 0,
                        MatchedFieldCount = result.MatchedFieldCount,
                        ConflictingFieldCount = result.ConflictingFieldCount,
                        CreatedAt = now,
                    };
                    entityMatches.Add(entityMatch);
                    foreach (var field in result.Fields)
                    {
                        field.TenantId = tenantId;
                        field.ArtifactEntityMatchId = entityMatch.Id;
                        fields.Add(field);
                    }
                }
            }

            run.Status = ResolveRunStatus(processedEntityTypes.Count, successfulProviders, providerFailures.Count);
            if (providerFailures.Count > 0)
            {
                run.FailureCode = providerFailures
                    .Select(item => item[(item.IndexOf(':') + 1)..])
                    .Distinct(StringComparer.Ordinal)
                    .FirstOrDefault();
                run.FailureMessage = string.Join(";", providerFailures);
            }

            var duplicateSignals = configuration.EnableDuplicateDetection
                ? await DetectDuplicatesAsync(
                    tenantId,
                    artifact,
                    run,
                    profileDocument,
                    sourceFacts,
                    entityMatches,
                    cancellationToken)
                : [];

            run.CompletedAt = DateTimeOffset.UtcNow;
            run.UpdatedAt = DateTimeOffset.UtcNow;
            if (run.Status == MatchRunStatuses.Failed)
            {
                await repository.SaveAsync(cancellationToken);
                await RecordAuditAsync(
                    "MATCHING_FAILED",
                    run,
                    processedEntityTypes,
                    candidateCount,
                    duplicateSignals.Count,
                    actorId,
                    correlationId,
                    cancellationToken);
                return Map(run);
            }

            await repository.FinalizeCurrentAsync(
                tenantId,
                artifactId,
                run,
                entityMatches,
                fields,
                duplicateSignals,
                cancellationToken);
            await RecordAuditAsync(
                "MATCHING_COMPLETED",
                run,
                processedEntityTypes,
                candidateCount,
                duplicateSignals.Count,
                actorId,
                correlationId,
                cancellationToken);
            return Map(run, entityMatches, duplicateSignals);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            run.Status = MatchRunStatuses.Failed;
            run.FailureCode = MatchingFailureCodes.ExecutionCancelled;
            run.FailureMessage = "Matching execution was cancelled.";
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.UpdatedAt = DateTimeOffset.UtcNow;
            try
            {
                await repository.SaveAsync(CancellationToken.None);
                await RecordAuditAsync(
                    "MATCHING_CANCELLED",
                    run,
                    [],
                    0,
                    0,
                    actorId,
                    correlationId,
                    CancellationToken.None);
            }
            catch (Exception persistException)
            {
                logger.LogWarning(
                    persistException,
                    "Unable to persist cancelled matching run. Tenant={TenantId} Artifact={ArtifactId} MatchRun={MatchRunId}",
                    tenantId,
                    artifactId,
                    run.Id);
            }

            throw;
        }
        catch (Exception ex)
        {
            run.Status = MatchRunStatuses.Failed;
            run.FailureCode = MatchingFailureCodes.ScoringFailed;
            run.FailureMessage = "Matching execution failed.";
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.UpdatedAt = DateTimeOffset.UtcNow;
            await repository.SaveAsync(cancellationToken);
            await RecordAuditAsync(
                "MATCHING_FAILED",
                run,
                [],
                0,
                0,
                actorId,
                correlationId,
                cancellationToken);
            logger.LogError(
                ex,
                "Tenant matching failed. Tenant={TenantId} Artifact={ArtifactId} MatchRun={MatchRunId}",
                tenantId,
                artifactId,
                run.Id);
            return Map(run);
        }
    }

    private async Task<CurrentNormalizationContext?> LoadCurrentNormalizationAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var artifact = await classificationRepository.FindArtifactAsync(
            tenantId,
            artifactId,
            cancellationToken);
        if (artifact is null)
            return null;

        var classification = await classificationRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            cancellationToken);
        if (classification is null)
            return null;

        var extraction = await extractionRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            classification.Id,
            cancellationToken);
        if (extraction is null ||
            !string.Equals(extraction.Status, ExtractionStatuses.Completed, StringComparison.Ordinal))
            return null;

        var normalization = await normalizationRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            extraction.Id,
            cancellationToken);
        if (normalization is null ||
            normalization.Status is not
                (NormalizationRunStatuses.Completed or NormalizationRunStatuses.Partial))
            return null;

        return new CurrentNormalizationContext(artifact, extraction, normalization);
    }

    private async Task<IReadOnlyList<ArtifactDuplicateSignal>> DetectDuplicatesAsync(
        Guid tenantId,
        IntakeArtifact artifact,
        ArtifactMatchRun run,
        MatchingProfileDocument profile,
        IReadOnlyList<MatchDiscoveryFact> sourceFacts,
        IReadOnlyList<ArtifactEntityMatch> entityMatches,
        CancellationToken cancellationToken)
    {
        var signals = new List<ArtifactDuplicateSignal>();
        if (!string.IsNullOrWhiteSpace(artifact.Sha256))
        {
            var related = await artifactRepository.ListBySha256Async(
                tenantId,
                artifact.Sha256,
                artifact.Id,
                cancellationToken);
            foreach (var duplicate in related)
            {
                signals.Add(new ArtifactDuplicateSignal
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ArtifactMatchRunId = run.Id,
                    DuplicateType = DuplicateTypes.ExactArtifactDuplicate,
                    RelatedArtifactId = duplicate.Id,
                    Score = 1m,
                    Status = DuplicateStatuses.ConfirmedSignal,
                    ReasonCode = MatchReasonCodes.ExactArtifactHash,
                    EvidenceJson = JsonSerializer.Serialize(new
                    {
                        sameSha256 = true,
                        currentSourceType = artifact.ArtifactSourceType,
                        relatedSourceType = duplicate.ArtifactSourceType,
                    }, JsonOptions),
                    CreatedAt = DateTimeOffset.UtcNow,
                });

                if (IsCrossSourceDuplicate(artifact, duplicate))
                {
                    signals.Add(new ArtifactDuplicateSignal
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ArtifactMatchRunId = run.Id,
                        DuplicateType = DuplicateTypes.ContentDuplicate,
                        RelatedArtifactId = duplicate.Id,
                        Score = 1m,
                        Status = DuplicateStatuses.ConfirmedSignal,
                        ReasonCode = MatchReasonCodes.ExactArtifactHash,
                        EvidenceJson = JsonSerializer.Serialize(new
                        {
                            sameSha256 = true,
                            crossSource = true,
                        }, JsonOptions),
                        CreatedAt = DateTimeOffset.UtcNow,
                    });
                }
            }
        }

        if (profile.PrimaryDuplicateRule is not null)
        {
            var rule = profile.PrimaryDuplicateRule;
            var topEntities = entityMatches
                .Where(match => match.IsTopCandidate &&
                                rule.RequiredEntityTypes.Contains(
                                    match.EntityType,
                                    StringComparer.Ordinal) &&
                                match.ConflictingFieldCount == 0 &&
                                match.MatchStatus is MatchStatuses.Strong or MatchStatuses.Possible &&
                                match.Fields.Any(field =>
                                    field.EffectiveWeight > 0 &&
                                    field.MatchOutcome is MatchOutcomes.Exact or
                                        MatchOutcomes.NormalizedExact))
                .ToDictionary(match => match.EntityType);
            var requiredFacts = rule.RequiredFactCodes
                .Select(code => sourceFacts.FirstOrDefault(fact =>
                    MatchScoring.FactCodeMatches(code, fact.FactCode) &&
                    fact.ValidationStatus == ValidationStatuses.Valid &&
                    !string.IsNullOrWhiteSpace(fact.ComparisonKey)))
                .ToArray();
            if (requiredFacts.All(fact => fact is not null) &&
                rule.RequiredEntityTypes.All(topEntities.ContainsKey))
            {
                var fingerprint = BuildBusinessFingerprint(rule, topEntities, requiredFacts!);
                run.BusinessKeyFingerprint = fingerprint;
                run.BusinessDuplicateRuleCode = rule.Code;
                var prior = await repository.FindBusinessDuplicateRunAsync(
                    tenantId,
                    fingerprint,
                    artifact.Id,
                    cancellationToken);
                if (prior is not null)
                {
                    signals.Add(new ArtifactDuplicateSignal
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ArtifactMatchRunId = run.Id,
                        DuplicateType = rule.DuplicateType,
                        RelatedArtifactId = prior.IntakeArtifactId,
                        RelatedBusinessEntityType = MatchingEntityTypes.Patient,
                        RelatedBusinessEntityId = topEntities
                            .GetValueOrDefault(MatchingEntityTypes.Patient)
                            ?.CandidateEntityId,
                        Score = rule.Score,
                        Status = rule.Status,
                        ReasonCode = MatchReasonCodes.BusinessKeyExact,
                        EvidenceJson = JsonSerializer.Serialize(new
                        {
                            rule = rule.Code,
                            requiredFactCodes = rule.RequiredFactCodes,
                            requiredEntityTypes = rule.RequiredEntityTypes,
                        }, JsonOptions),
                        CreatedAt = DateTimeOffset.UtcNow,
                    });
                }
            }
        }

        return signals;
    }

    private static string BuildBusinessFingerprint(
        MatchingDuplicateRule rule,
        IReadOnlyDictionary<string, ArtifactEntityMatch> topEntities,
        IReadOnlyList<MatchDiscoveryFact?> facts)
    {
        var material = string.Join(
            "|",
            new[] { rule.Code }
                .Concat(rule.RequiredEntityTypes
                    .OrderBy(type => type, StringComparer.Ordinal)
                    .Select(type => $"{type}:{topEntities[type].CandidateEntityId:D}"))
                .Concat(rule.RequiredFactCodes
                    .Select((code, index) => $"{code}:{facts[index]!.ComparisonKey}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static bool IsCrossSourceDuplicate(IntakeArtifact left, IntakeArtifact right)
    {
        var sourceTypes = new[] { left.ArtifactSourceType, right.ArtifactSourceType }
            .Select(value => value.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
        return sourceTypes.Contains("EMAIL") && sourceTypes.Contains("MANUAL");
    }

    private static string BuildExecutionKey(
        ArtifactNormalization normalization,
        MatchingProfileDefinition profile,
        MatchingProfileDocument document)
    {
        var material = string.Join(
            "|",
            normalization.Id,
            normalization.ExecutionKey,
            profile.Code,
            profile.Version,
            profile.ScoringVersion,
            string.Join(",", document.EntityTypes));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static string ResolveRunStatus(
        int processedEntityTypes,
        int successfulProviders,
        int failedProviders) =>
        failedProviders > 0
            ? successfulProviders > 0
                ? MatchRunStatuses.Partial
                : MatchRunStatuses.Failed
            : processedEntityTypes > 0
                ? MatchRunStatuses.Completed
                : MatchRunStatuses.Failed;

    private async Task RecordAuditAsync(
        string action,
        ArtifactMatchRun run,
        IReadOnlyList<string> entityTypes,
        int candidateCount,
        int duplicateCount,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        await auditSink.RecordAsync(
            new MatchingAuditEntry(
                action,
                run.TenantId,
                run.IntakeArtifactId,
                run.ArtifactNormalizationId,
                run.Id,
                entityTypes,
                candidateCount,
                duplicateCount,
                run.Status,
                run.FailureCode,
                correlationId,
                actorId),
            cancellationToken);
    }

    private static ArtifactMatchResponse Map(ArtifactMatchRun run) =>
        Map(run, run.EntityMatches, run.DuplicateSignals);

    private static ArtifactMatchResponse Map(
        ArtifactMatchRun run,
        IEnumerable<ArtifactEntityMatch> entityMatches,
        IEnumerable<ArtifactDuplicateSignal> duplicateSignals) =>
        new(
            run.Id,
            run.IntakeArtifactId,
            run.ArtifactNormalizationId,
            run.MatchingProfileCode,
            run.MatchingProfileVersion,
            run.ScoringVersion,
            run.Status,
            run.IsCurrent,
            run.FailureCode,
            run.FailureMessage,
            run.RequestedAt,
            run.CompletedAt,
            entityMatches
                .OrderBy(match => match.EntityType)
                .ThenBy(match => match.Rank)
                .Select(match => new EntityMatchResponse(
                    match.Id,
                    match.EntityType,
                    match.CandidateEntityId,
                    match.CandidateDisplayLabel,
                    match.Score,
                    match.Rank,
                    match.MatchStatus,
                    match.IsTopCandidate,
                    match.MatchedFieldCount,
                    match.ConflictingFieldCount,
                    match.Fields
                        .OrderBy(field => field.FactCode)
                        .Select(field => new MatchFieldResponse(
                            field.SourceNormalizedFactId,
                            field.FactCode,
                            field.CandidateFieldName,
                            field.ComparisonMethod,
                            field.MatchOutcome,
                            field.FieldScore,
                            field.Weight,
                            field.EffectiveWeight,
                            field.WeightedScore,
                            field.ReasonCode))
                        .ToArray()))
                .ToArray(),
            duplicateSignals
                .OrderByDescending(signal => signal.Score)
                .ThenBy(signal => signal.RelatedArtifactId)
                .Select(signal => new DuplicateSignalResponse(
                    signal.Id,
                    signal.DuplicateType,
                    signal.RelatedArtifactId,
                    signal.RelatedBusinessEntityType,
                    signal.RelatedBusinessEntityId,
                    signal.Score,
                    signal.Status,
                    signal.ReasonCode))
                .ToArray());

    private static bool IsUsable(string validationStatus) =>
        validationStatus is ValidationStatuses.Valid or ValidationStatuses.Ambiguous;

    private sealed record CurrentNormalizationContext(
        IntakeArtifact Artifact,
        ArtifactExtraction Extraction,
        ArtifactNormalization Normalization);
}