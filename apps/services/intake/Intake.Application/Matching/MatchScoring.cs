using Intake.Domain.Matching;

namespace Intake.Application.Matching;

public sealed record CandidateScoreResult(
    TenantMatchCandidate Candidate,
    decimal Score,
    string Status,
    int MatchedFieldCount,
    int ConflictingFieldCount,
    bool HasHardConflict,
    bool HasHardMatch,
    IReadOnlyList<ArtifactMatchField> Fields);

public static class MatchScoring
{
    public static CandidateScoreResult Score(
        string entityType,
        MatchingEntityRule rule,
        TenantMatchCandidate candidate,
        IReadOnlyList<MatchDiscoveryFact> sourceFacts,
        bool useSourceConfidence)
    {
        var fields = new List<ArtifactMatchField>();
        decimal positive = 0;
        decimal penalties = 0;
        decimal denominator = 0;
        var matched = 0;
        var conflicts = 0;
        var hardConflict = false;
        var hardMatch = false;
        var now = DateTimeOffset.UtcNow;

        foreach (var fieldRule in rule.Fields)
        {
            var matchingFacts = sourceFacts
                .Where(fact => FactCodeMatches(fieldRule.FactCode, fact.FactCode))
                .ToArray();
            var source = matchingFacts
                .OrderBy(fact => IsUsable(fact.ValidationStatus) ? 0 : 1)
                .ThenBy(fact => fact.SourceNormalizedFactId)
                .FirstOrDefault();
            if (source is null)
                continue;

            var hasCandidateValue =
                candidate.Fields.TryGetValue(fieldRule.CandidateFieldName, out var candidateValue) &&
                candidateValue is not null &&
                (!string.IsNullOrWhiteSpace(candidateValue.ComparisonKey) ||
                 !string.IsNullOrWhiteSpace(candidateValue.Value));
            if (hasCandidateValue)
            {
                source = matchingFacts
                    .Where(fact => IsUsable(fact.ValidationStatus) &&
                                   (!string.IsNullOrWhiteSpace(fact.ComparisonKey) ||
                                    !string.IsNullOrWhiteSpace(fact.NormalizedValue)))
                    .Select(fact => (Fact: fact, Comparison: Compare(
                        entityType,
                        fieldRule,
                        fact,
                        candidateValue!)))
                    .OrderByDescending(item => item.Comparison.Score)
                    .ThenBy(item => item.Fact.SourceNormalizedFactId)
                    .Select(item => item.Fact)
                    .FirstOrDefault() ?? source;
            }

            var effectiveWeight = fieldRule.Weight *
                (useSourceConfidence ? Clamp((decimal)source.SourceConfidence) : 1m);
            if (string.Equals(source.ValidationStatus, "AMBIGUOUS", StringComparison.Ordinal))
                effectiveWeight *= 0.50m;

            var field = new ArtifactMatchField
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty,
                SourceNormalizedFactId = source.SourceNormalizedFactId,
                FactCode = fieldRule.FactCode,
                CandidateFieldName = fieldRule.CandidateFieldName,
                ComparisonMethod = fieldRule.ComparisonMethod,
                Weight = Round(fieldRule.Weight),
                EffectiveWeight = Round(effectiveWeight),
                CreatedAt = now,
            };

            if (!IsUsable(source.ValidationStatus))
            {
                field.MatchOutcome = MatchOutcomes.NotApplicable;
                field.ReasonCode = string.Equals(source.ValidationStatus, "AMBIGUOUS", StringComparison.Ordinal)
                    ? MatchReasonCodes.SourceValueAmbiguous
                    : MatchReasonCodes.SourceValueInvalid;
                field.EffectiveWeight = 0;
                fields.Add(field);
                continue;
            }

            if (!hasCandidateValue)
            {
                field.MatchOutcome = MatchOutcomes.Missing;
                field.ReasonCode = MatchReasonCodes.CandidateValueMissing;
                field.EffectiveWeight = 0;
                fields.Add(field);
                continue;
            }

            if (string.IsNullOrWhiteSpace(source.ComparisonKey) &&
                string.IsNullOrWhiteSpace(source.NormalizedValue))
            {
                field.MatchOutcome = MatchOutcomes.NotApplicable;
                field.ReasonCode = MatchReasonCodes.SourceValueMissing;
                field.EffectiveWeight = 0;
                fields.Add(field);
                continue;
            }

            var comparison = Compare(
                entityType,
                fieldRule,
                source,
                candidateValue!);

            field.FieldScore = Round(comparison.Score);
            field.MatchOutcome = comparison.Outcome;
            field.ReasonCode = comparison.ReasonCode;
            denominator += effectiveWeight;
            positive += effectiveWeight * comparison.Score;
            if (comparison.Outcome == MatchOutcomes.Conflict)
            {
                conflicts++;
                penalties += effectiveWeight * fieldRule.ConflictPenalty;
                hardConflict |= fieldRule.HardConflict;
            }
            else if (comparison.Score > 0)
            {
                matched++;
                hardMatch |= fieldRule.HardConflict;
            }

            field.WeightedScore = Round(effectiveWeight * comparison.Score);
            fields.Add(field);
        }

        var score = denominator <= 0
            ? 0m
            : Clamp((positive - penalties) / denominator);
        if (hardConflict)
            score = Math.Min(score, rule.HardConflictMaximumScore);

        var status = DetermineStatus(
            score,
            matched,
            conflicts,
            hardConflict,
            hardMatch,
            rule,
            denominator > 0);

        return new CandidateScoreResult(
            candidate,
            Round(score),
            status,
            matched,
            conflicts,
            hardConflict,
            hardMatch,
            fields);
    }

    private static ComparisonResult Compare(
        string entityType,
        MatchingFieldRule rule,
        MatchDiscoveryFact source,
        TenantMatchCandidateField candidate)
    {
        var sourceKey = source.ComparisonKey ?? source.NormalizedValue ?? string.Empty;
        var candidateKey = candidate.ComparisonKey ?? candidate.Value ?? string.Empty;
        if (string.Equals(sourceKey.Trim(), candidateKey.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return new(
                1m,
                MatchOutcomes.NormalizedExact,
                ExactReason(entityType, rule.FactCode));
        }

        if (rule.ComparisonMethod is "PERSON_NAME" or "ORGANIZATION" or "ADDRESS")
        {
            var similarity = StringSimilarity.Score(sourceKey, candidateKey);
            if (similarity >= 0.80m)
            {
                return new(
                    similarity,
                    MatchOutcomes.Fuzzy,
                    FuzzyReason(entityType, rule.ComparisonMethod));
            }

            if (similarity >= 0.45m)
            {
                return new(
                    similarity,
                    MatchOutcomes.Partial,
                    rule.ComparisonMethod == "ADDRESS"
                        ? MatchReasonCodes.AddressFuzzy
                        : FuzzyReason(entityType, rule.ComparisonMethod));
            }
        }

        return new(
            0m,
            MatchOutcomes.Conflict,
            ConflictReason(rule.FactCode));
    }

    private static string DetermineStatus(
        decimal score,
        int matched,
        int conflicts,
        bool hardConflict,
        bool hardMatch,
        MatchingEntityRule rule,
        bool hasComparableData)
    {
        if (!hasComparableData || matched == 0 && conflicts == 0)
            return MatchStatuses.InsufficientData;
        if (hardConflict)
            return MatchStatuses.Conflicted;
        if (score >= rule.StrongThreshold &&
            matched >= rule.StrongMinimumMatchedFields &&
            (!rule.StrongRequiresHardIdentifier || hardMatch))
            return MatchStatuses.Strong;
        if (score >= rule.PossibleThreshold)
            return MatchStatuses.Possible;
        return conflicts > 0 ? MatchStatuses.Conflicted : MatchStatuses.Weak;
    }

    private static bool IsUsable(string validationStatus) =>
        validationStatus is "VALID" or "AMBIGUOUS";

    public static bool FactCodeMatches(string configuredFactCode, string actualFactCode) =>
        string.Equals(configuredFactCode, actualFactCode, StringComparison.Ordinal) ||
        configuredFactCode switch
        {
            "PATIENT_NAME" => actualFactCode is "PATIENT_FULL_NAME",
            "PATIENT_IDENTIFIER" => actualFactCode is "MEDICAL_RECORD_NUMBER",
            "ACCOUNT_NUMBER" => actualFactCode is "PATIENT_ACCOUNT_NUMBER",
            _ => false,
        };

    private static string ExactReason(string entityType, string factCode) =>
        factCode switch
        {
            "DATE_OF_BIRTH" or "DATE_OF_ACCIDENT" or "DATE_OF_SERVICE_START" =>
                MatchReasonCodes.DobExact,
            "ATTORNEY_EMAIL" => MatchReasonCodes.EmailExact,
            "PROVIDER_PHONE" or "ATTORNEY_PHONE" => MatchReasonCodes.PhoneExact,
            "PATIENT_IDENTIFIER" or "ACCOUNT_NUMBER" or "CASE_NUMBER" or "CLAIM_NUMBER" =>
                MatchReasonCodes.IdentifierExact,
            "FACILITY_ADDRESS" => MatchReasonCodes.AddressExact,
            _ when entityType is MatchingEntityTypes.Patient or MatchingEntityTypes.Attorney =>
                MatchReasonCodes.NameExact,
            _ => MatchReasonCodes.OrganizationExact,
        };

    private static string FuzzyReason(string entityType, string comparisonMethod) =>
        comparisonMethod switch
        {
            "PERSON_NAME" => MatchReasonCodes.NameFuzzy,
            "ORGANIZATION" => MatchReasonCodes.OrganizationFuzzy,
            "ADDRESS" => MatchReasonCodes.AddressFuzzy,
            _ => entityType,
        };

    private static string ConflictReason(string factCode) =>
        factCode switch
        {
            "DATE_OF_BIRTH" or "DATE_OF_ACCIDENT" or "DATE_OF_SERVICE_START" =>
                MatchReasonCodes.DobConflict,
            "PATIENT_IDENTIFIER" or "ACCOUNT_NUMBER" or "CASE_NUMBER" or "CLAIM_NUMBER" =>
                MatchReasonCodes.IdentifierConflict,
            _ => MatchReasonCodes.IdentifierConflict,
        };

    private static decimal Clamp(decimal value) => Math.Clamp(value, 0m, 1m);
    private static decimal Round(decimal value) => decimal.Round(Clamp(value), 5);

    private readonly record struct ComparisonResult(
        decimal Score,
        string Outcome,
        string ReasonCode);
}

public static class StringSimilarity
{
    public static decimal Score(string left, string right)
    {
        var a = Compact(left);
        var b = Compact(right);
        if (a.Length == 0 || b.Length == 0)
            return 0m;
        if (string.Equals(a, b, StringComparison.Ordinal))
            return 1m;

        var distance = Levenshtein(a, b);
        return decimal.Round(
            Math.Max(0m, 1m - (decimal)distance / Math.Max(a.Length, b.Length)),
            5);
    }

    private static string Compact(string value) =>
        new(value
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static int Levenshtein(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}