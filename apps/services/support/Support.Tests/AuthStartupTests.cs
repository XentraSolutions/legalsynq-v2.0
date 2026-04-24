using FluentAssertions;
using Support.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Support.Tests;

/// <summary>
/// Verifies AuthExtensions.AddSupportAuth is fail-closed: in any non-Testing
/// environment, missing/insecure JWT configuration must throw at startup
/// rather than register a JWT pipeline that silently accepts attacker tokens.
/// </summary>
public class AuthStartupTests
{
    private sealed class FakeEnv : IWebHostEnvironment
    {
        public FakeEnv(string name) { EnvironmentName = name; }
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Support.Api";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static IConfiguration Cfg(Dictionary<string, string?> kv) =>
        new ConfigurationBuilder().AddInMemoryCollection(kv).Build();

    [Fact]
    public void Production_With_No_Jwt_Config_Throws()
    {
        var act = () => new ServiceCollection().AddSupportAuth(
            Cfg(new Dictionary<string, string?>()), new FakeEnv("Production"));
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Authentication:Jwt is not configured*");
    }

    [Fact]
    public void Production_With_Both_Authority_And_SymmetricKey_Throws()
    {
        var act = () => new ServiceCollection().AddSupportAuth(Cfg(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Authority"] = "https://idp.example.com",
            ["Authentication:Jwt:SymmetricKey"] = new string('k', 64),
            ["Authentication:Jwt:Issuer"] = "iss",
            ["Authentication:Jwt:Audience"] = "aud"
        }), new FakeEnv("Production"));
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*both Authority and SymmetricKey*");
    }

    [Fact]
    public void Production_With_Symmetric_Missing_Audience_Throws()
    {
        var act = () => new ServiceCollection().AddSupportAuth(Cfg(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SymmetricKey"] = new string('k', 64),
            ["Authentication:Jwt:Issuer"] = "iss"
        }), new FakeEnv("Production"));
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Audience*required*");
    }

    [Fact]
    public void Production_With_Symmetric_Missing_Issuer_Throws()
    {
        var act = () => new ServiceCollection().AddSupportAuth(Cfg(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SymmetricKey"] = new string('k', 64),
            ["Authentication:Jwt:Audience"] = "aud"
        }), new FakeEnv("Production"));
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Issuer*required*");
    }

    [Fact]
    public void Production_With_Short_SymmetricKey_Throws()
    {
        var act = () => new ServiceCollection().AddSupportAuth(Cfg(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SymmetricKey"] = "tooshort",
            ["Authentication:Jwt:Issuer"] = "iss",
            ["Authentication:Jwt:Audience"] = "aud"
        }), new FakeEnv("Production"));
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*at least 32 bytes*");
    }

    [Fact]
    public void Production_With_Valid_Symmetric_Config_Succeeds()
    {
        var act = () => new ServiceCollection().AddSupportAuth(Cfg(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SymmetricKey"] = new string('k', 64),
            ["Authentication:Jwt:Issuer"] = "iss",
            ["Authentication:Jwt:Audience"] = "aud"
        }), new FakeEnv("Production"));
        act.Should().NotThrow();
    }

    [Fact]
    public void Production_With_Valid_Authority_Config_Succeeds()
    {
        var act = () => new ServiceCollection().AddSupportAuth(Cfg(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Authority"] = "https://idp.example.com",
            ["Authentication:Jwt:Issuer"] = "https://idp.example.com",
            ["Authentication:Jwt:Audience"] = "aud"
        }), new FakeEnv("Production"));
        act.Should().NotThrow();
    }

    [Fact]
    public void Testing_Environment_Skips_Jwt_Validation()
    {
        // In Testing, AddSupportAuth registers TestAuthHandler instead of JWT,
        // so missing JWT config must not throw.
        var act = () => new ServiceCollection().AddSupportAuth(
            Cfg(new Dictionary<string, string?>()), new FakeEnv("Testing"));
        act.Should().NotThrow();
    }
}
