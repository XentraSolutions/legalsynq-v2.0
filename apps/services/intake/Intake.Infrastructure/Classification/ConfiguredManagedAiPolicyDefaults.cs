using Intake.Application.Classification;
using Microsoft.Extensions.Options;

namespace Intake.Infrastructure.Classification;

public sealed class ConfiguredManagedAiPolicyDefaults(
    IOptions<SynqAiOptions> options) : IManagedAiPolicyDefaults
{
    public ManagedAiPolicyDefaults Current
    {
        get
        {
            var configured = options.Value;
            return new ManagedAiPolicyDefaults(
                configured.ManagedProviderCode.Trim().ToUpperInvariant(),
                configured.ManagedModelCode.Trim(),
                configured.ManagedCredentialReference.Trim());
        }
    }
}