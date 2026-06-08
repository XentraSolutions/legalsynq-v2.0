using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Flow.UnitTests;

/// <summary>
/// LS-FLOW-HARDEN-A1 — verifies that <c>AddServiceTokenBearer</c>
/// fails fast on startup when <paramref name="failFastIfMissingSecret"/>
/// is set and the signing material is missing or too short. This is the
/// guard wired into Flow.Api outside the Development environment.
/// </summary>
public class ServiceTokenStartupGuardTests
{
    [Fact]
    public void Missing_secret_throws_when_failFast_is_true()
    {
        var cfg = BuildConfig(signingKey: null);
        var auth = NewAuth();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            auth.AddServiceTokenBearer(cfg, failFastIfMissingSecret: true));

        Assert.Contains("no signing key is configured", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Short_secret_throws_when_failFast_is_true()
    {
        var cfg = BuildConfig(signingKey: "tooshort");
        var auth = NewAuth();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            auth.AddServiceTokenBearer(cfg, failFastIfMissingSecret: true));

        Assert.Contains("at least 32 characters", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sufficient_secret_does_not_throw_when_failFast_is_true()
    {
        // 32+ chars
        var cfg = BuildConfig(signingKey: new string('k', 48));
        var auth = NewAuth();

        var ex = Record.Exception(() =>
            auth.AddServiceTokenBearer(cfg, failFastIfMissingSecret: true));

        Assert.Null(ex);
    }

    [Fact]
    public void Missing_secret_does_not_throw_when_failFast_is_false()
    {
        // Development behaviour — the scheme self-disables (no token can validate)
        // but startup still succeeds so local dev doesn't require config.
        var cfg = BuildConfig(signingKey: null);
        var auth = NewAuth();

        var ex = Record.Exception(() =>
            auth.AddServiceTokenBearer(cfg, failFastIfMissingSecret: false));

        Assert.Null(ex);
    }

    [Fact]
    public void Env_secret_overrides_config_secret_for_issuer_options()
    {
        const string configSecret = "config-secret-should-not-win-1234567890";
        const string envSecret = "env-secret-should-win-12345678901234567890";

        Environment.SetEnvironmentVariable(
            ServiceTokenAuthenticationDefaults.SecretEnvVar,
            envSecret);

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ServiceTokenOptions.SectionName}:SigningKey"] = configSecret
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceTokenIssuer(cfg, "test-service");

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<ServiceTokenOptions>>().Value;

        Assert.Equal(envSecret, options.SigningKey);
    }

    // ---------------- helpers ----------------

    private static IConfiguration BuildConfig(string? signingKey)
    {
        var dict = new Dictionary<string, string?>();
        if (signingKey is not null)
        {
            dict[$"{ServiceTokenOptions.SectionName}:SigningKey"] = signingKey;
        }
        // Make sure the env var fallback can't accidentally satisfy the check.
        Environment.SetEnvironmentVariable(ServiceTokenAuthenticationDefaults.SecretEnvVar, null);
        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    private static AuthenticationBuilder NewAuth()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.AddAuthentication();
    }
}
