namespace Intake.Contracts.Configuration;

public sealed class LienIntakeV1Configuration
{
    public bool RequireHumanReview { get; set; } = true;
    public bool AllowAutoApproval { get; set; }

    public double AutoApproveThreshold { get; set; } = 0.95;
    public double ReviewThreshold { get; set; } = 0.75;
    public double RejectThreshold { get; set; } = 0.50;

    public bool EnablePatientMatching { get; set; }
    public bool EnableCaseMatching { get; set; }
    public bool EnableFacilityMatching { get; set; }
    public bool EnableDuplicateDetection { get; set; }

    public bool ProcessAttachments { get; set; } = true;
    public bool ProcessEmailBody { get; set; } = true;
    public bool AllowUnsupportedDocuments { get; set; }

    public string? DestinationAdapterCode { get; set; }
}