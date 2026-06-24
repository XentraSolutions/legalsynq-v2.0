using System.IdentityModel.Tokens.Jwt;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.Extensions.Options;
using Xunit;

namespace BuildingBlocks.Tests;

public class ServiceTokenIssuerTests
{
    [Fact]
    public void IssueToken_UsesConfiguredAudience_WhenOverrideMissing()
    {
        var issuer = CreateIssuer("flow-service");

        var token = issuer.IssueToken(Guid.NewGuid().ToString());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("flow-service", jwt.Audiences.Single());
    }

    [Fact]
    public void IssueToken_UsesPerCallAudienceOverride_WhenProvided()
    {
        var issuer = CreateIssuer("flow-service");

        var token = issuer.IssueToken(Guid.NewGuid().ToString(), audience: "documents-service");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("documents-service", jwt.Audiences.Single());
    }

    private static ServiceTokenIssuer CreateIssuer(string audience)
    {
        var options = Options.Create(new ServiceTokenOptions
        {
            SigningKey = "dev-flow-service-token-signing-key-32chars!",
            Issuer = ServiceTokenAuthenticationDefaults.DefaultIssuer,
            Audience = audience,
            ServiceName = "test-service",
            LifetimeMinutes = 10,
        });

        return new ServiceTokenIssuer(options);
    }
}
