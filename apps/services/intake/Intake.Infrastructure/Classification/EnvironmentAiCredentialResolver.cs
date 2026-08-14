using Intake.Application.Classification;
using Microsoft.Extensions.Configuration;

namespace Intake.Infrastructure.Classification;

/// <summary>
/// Resolves only references, never persisted values. Deployments can back these
/// references with the platform secret/configuration boundary.
/// </summary>
public sealed class EnvironmentAiCredentialResolver(
    IConfiguration configuration) : IAiCredentialResolver
{
    public Task<string?> ResolveAsync(
        Guid tenantId,
        string credentialReference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (credentialReference.StartsWith("secret://env/", StringComparison.OrdinalIgnoreCase))
        {
            var key = credentialReference["secret://env/".Length..];
            if (key.Length > 0 && key.All(character =>
                    char.IsLetterOrDigit(character) || character is '_' or '-'))
                return Task.FromResult(configuration[key]);
        }

        if (credentialReference.StartsWith("secret://platform/", StringComparison.OrdinalIgnoreCase))
        {
            var key = credentialReference["secret://platform/".Length..];
            if (key.Length > 0 && key.All(character =>
                    char.IsLetterOrDigit(character) || character is '_' or '-'))
                return Task.FromResult(
                    configuration[$"SynqAi:PlatformCredentials:{key}"]);
        }

        var tenantPrefix = $"secret://tenant/{tenantId:D}/";
        if (credentialReference.StartsWith(tenantPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var key = credentialReference[tenantPrefix.Length..];
            if (key.Length > 0 && key.All(character =>
                    char.IsLetterOrDigit(character) || character is '_' or '-'))
                return Task.FromResult(
                    configuration[$"SynqAi:TenantCredentials:{tenantId:D}:{key}"]);
        }

        return Task.FromResult<string?>(null);
    }
}