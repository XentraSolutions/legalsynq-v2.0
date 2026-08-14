using Intake.Domain.Matching;

namespace Intake.Application.Matching;

public sealed record MatchDiscoveryFact(
    Guid SourceNormalizedFactId,
    string FactCode,
    string? NormalizedValue,
    string? ComparisonKey,
    string ValidationStatus,
    double SourceConfidence);

public sealed record TenantMatchCandidateField(
    string? Value,
    string? ComparisonKey,
    string? DataType = null);

public sealed record TenantMatchCandidate(
    Guid EntityId,
    string DisplayLabel,
    IReadOnlyDictionary<string, TenantMatchCandidateField> Fields);

public sealed record CandidateProviderResult(
    bool Succeeded,
    IReadOnlyList<TenantMatchCandidate> Candidates,
    string? FailureCode = null,
    string? FailureMessage = null)
{
    public static CandidateProviderResult Success(IReadOnlyList<TenantMatchCandidate> candidates) =>
        new(true, candidates);

    public static CandidateProviderResult Failure(string code, string message) =>
        new(false, [], code, message);
}

public interface ITenantMatchCandidateProvider
{
    string EntityType { get; }

    Task<CandidateProviderResult> SearchAsync(
        Guid tenantId,
        IReadOnlyList<MatchDiscoveryFact> facts,
        int maxCandidateSearchPool,
        CancellationToken cancellationToken);
}

public interface IMatchCandidateProviderRegistry
{
    IReadOnlyList<ITenantMatchCandidateProvider> Providers { get; }

    ITenantMatchCandidateProvider? Find(string entityType);
}

public interface ITenantMatchCandidateSource
{
    Task<CandidateProviderResult> SearchAsync(
        Guid tenantId,
        string entityType,
        IReadOnlyList<MatchDiscoveryFact> facts,
        int maxCandidateSearchPool,
        CancellationToken cancellationToken);
}

public sealed class TenantMatchCandidateProvider(
    ITenantMatchCandidateSource source,
    string entityType) : ITenantMatchCandidateProvider
{
    public string EntityType { get; } = entityType;

    public Task<CandidateProviderResult> SearchAsync(
        Guid tenantId,
        IReadOnlyList<MatchDiscoveryFact> facts,
        int maxCandidateSearchPool,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return Task.FromResult(CandidateProviderResult.Failure(
                MatchingFailureCodes.TenantContextInvalid,
                "Tenant context is required for candidate discovery."));

        return source.SearchAsync(
            tenantId,
            EntityType,
            facts,
            maxCandidateSearchPool,
            cancellationToken);
    }
}

public sealed class MatchCandidateProviderRegistry(
    IEnumerable<ITenantMatchCandidateProvider> providers) : IMatchCandidateProviderRegistry
{
    public IReadOnlyList<ITenantMatchCandidateProvider> Providers { get; } =
        providers
            .GroupBy(provider => provider.EntityType, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(provider => provider.EntityType, StringComparer.Ordinal)
            .ToArray();

    public ITenantMatchCandidateProvider? Find(string entityType) =>
        Providers.FirstOrDefault(provider =>
            string.Equals(provider.EntityType, entityType, StringComparison.Ordinal));
}