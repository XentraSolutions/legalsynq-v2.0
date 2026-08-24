namespace Intake.Domain.Matching;

public sealed class ArtifactMatchRun
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid IntakeArtifactId { get; set; }
    public Guid ArtifactNormalizationId { get; set; }
    public string MatchingProfileCode { get; set; } = string.Empty;
    public int MatchingProfileVersion { get; set; }
    public string ScoringVersion { get; set; } = string.Empty;
    public string Status { get; set; } = MatchRunStatuses.Processing;
    public string ExecutionKey { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public string? CurrentResultMarker { get; set; }
    public string? BusinessKeyFingerprint { get; set; }
    public string? BusinessDuplicateRuleCode { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ArtifactEntityMatch> EntityMatches { get; set; } = [];
    public ICollection<ArtifactDuplicateSignal> DuplicateSignals { get; set; } = [];
}