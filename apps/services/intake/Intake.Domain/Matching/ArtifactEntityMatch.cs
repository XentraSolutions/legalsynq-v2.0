namespace Intake.Domain.Matching;

public sealed class ArtifactEntityMatch
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ArtifactMatchRunId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid CandidateEntityId { get; set; }
    public string CandidateDisplayLabel { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public int Rank { get; set; }
    public string MatchStatus { get; set; } = MatchStatuses.InsufficientData;
    public bool IsTopCandidate { get; set; }
    public int MatchedFieldCount { get; set; }
    public int ConflictingFieldCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<ArtifactMatchField> Fields { get; set; } = [];
}