using Microsoft.Extensions.Configuration;

namespace Xenia.Infrastructure.Assistant;

internal interface IAssistantSecretResolver
{
    string? ResolveSecret(string? secretReference);
}

internal sealed class AssistantSecretResolver : IAssistantSecretResolver
{
    private readonly IConfiguration _configuration;

    public AssistantSecretResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string? ResolveSecret(string? secretReference)
    {
        if (string.IsNullOrWhiteSpace(secretReference)) return null;

        var reference = secretReference.Trim();
        if (reference.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            var envName = reference["env:".Length..].Trim();
            return string.IsNullOrWhiteSpace(envName) ? null : Environment.GetEnvironmentVariable(envName);
        }

        if (reference.StartsWith("configuration:", StringComparison.OrdinalIgnoreCase))
        {
            var key = reference["configuration:".Length..].Trim();
            return string.IsNullOrWhiteSpace(key) ? null : _configuration[key];
        }

        return null;
    }
}
