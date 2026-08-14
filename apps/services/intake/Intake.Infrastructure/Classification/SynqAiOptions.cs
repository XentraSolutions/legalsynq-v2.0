using Intake.Domain.Classification;

namespace Intake.Infrastructure.Classification;

public sealed class SynqAiOptions
{
    public const string SectionName = "SynqAi";

    public string ManagedProviderCode { get; set; } = SynqAiProviderCodes.OpenAi;
    public string ManagedModelCode { get; set; } = "gpt-4o-mini";
    public string ManagedCredentialReference { get; set; } = "secret://platform/synq-ai";
    public OpenAiProviderOptions OpenAi { get; set; } = new();
    public int MaxTaxonomyCharacters { get; set; } = 32_000;
    public int MaxOutputTokens { get; set; } = 600;
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 3;
}

public sealed class OpenAiProviderOptions
{
    public string? BaseUrl { get; set; }
    public string? DefaultCredentialReference { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
}