using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Support.Tests;

internal static class TestJwt
{
    public const string Issuer = "https://test-issuer.local";
    public const string Audience = "support-api-tests";
    public const string Key = "test-only-symmetric-signing-key-for-prod-like-tests-32+chars!!";

    public static string Issue(
        string sub = "test-user",
        string? tenantId = null,
        IEnumerable<string>? roles = null)
    {
        var claims = new List<Claim> { new("sub", sub) };
        if (!string.IsNullOrEmpty(tenantId)) claims.Add(new Claim("tenant_id", tenantId));
        foreach (var r in roles ?? Array.Empty<string>()) claims.Add(new Claim("role", r));

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
