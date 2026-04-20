using Microsoft.Extensions.Options;

namespace Monitoring.Api.Authentication;

/// <summary>
/// Fail-fast validator for <see cref="JwtAuthenticationOptions"/>.
/// Used together with <c>ValidateOnStart()</c> so misconfigured services
/// never enter a partially-secured state.
/// </summary>
internal sealed class JwtAuthenticationOptionsValidator : IValidateOptions<JwtAuthenticationOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtAuthenticationOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            errors.Add($"'{JwtAuthenticationOptions.SectionName}:Issuer' is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            errors.Add($"'{JwtAuthenticationOptions.SectionName}:Audience' is required.");
        }

        var hasAuthority = !string.IsNullOrWhiteSpace(options.Authority);
        var hasPublicKey = !string.IsNullOrWhiteSpace(options.PublicKeyPem);

        if (!hasAuthority && !hasPublicKey)
        {
            errors.Add(
                $"Either '{JwtAuthenticationOptions.SectionName}:Authority' " +
                $"or '{JwtAuthenticationOptions.SectionName}:PublicKeyPem' must be configured " +
                "for RS256 token validation.");
        }

        if (options.ClockSkewSeconds < 0)
        {
            errors.Add($"'{JwtAuthenticationOptions.SectionName}:ClockSkewSeconds' must be >= 0.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
