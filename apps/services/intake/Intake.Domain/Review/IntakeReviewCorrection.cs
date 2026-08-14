namespace Intake.Domain.Review;

public sealed class IntakeReviewCorrection
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid IntakeReviewId { get; set; }
    public string TargetType { get; set; } = "FACT";
    public Guid? TargetId { get; set; }
    public string FactCode { get; set; } = string.Empty;
    public Guid? OriginalExtractedFactId { get; set; }
    public Guid? OriginalNormalizedFactId { get; set; }
    public string CorrectionType { get; set; } = IntakeReviewCorrectionTypes.ValueCorrection;
    public string? CorrectedValue { get; set; }
    public string? CorrectedJson { get; set; }
    public string? NormalizedValue { get; set; }
    public string? ValidationStatus { get; set; }
    public string SourceType { get; set; } = "HUMAN";
    public bool HumanVerified { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? SupersedesCorrectionId { get; set; }
}