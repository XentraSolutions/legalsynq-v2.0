namespace Intake.Application.Classification;

public sealed record ManagedAiPolicyDefaults(
    string ProviderCode,
    string ModelCode,
    string CredentialReference);

public interface IManagedAiPolicyDefaults
{
    ManagedAiPolicyDefaults Current { get; }
}