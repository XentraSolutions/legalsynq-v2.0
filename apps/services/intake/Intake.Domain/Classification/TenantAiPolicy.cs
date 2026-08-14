namespace Intake.Domain.Classification;

public sealed class TenantAiPolicy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public bool IsEnabled { get; set; }
    public string AccessMode { get; set; } = SynqAiAccessModes.LegalSynqManaged;
    public string ProviderCode { get; set; } = string.Empty;
    public string ModelCode { get; set; } = string.Empty;
    public string? CredentialReference { get; set; }
    public int MaxOutputTokens { get; set; } = 600;
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 3;
    public int PolicyVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}