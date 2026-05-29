using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Liens.Api.Tests.Helpers;
using Liens.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Liens.Api.Tests;

public static class JwtTokenHelper
{
    public const string Issuer    = "https://liens-tests.local";
    public const string Audience  = "liens-api-tests";
    public const string SigningKey = "liens-test-only-signing-key-32-plus-chars!!";

    private static readonly SymmetricSecurityKey _key =
        new(Encoding.UTF8.GetBytes(SigningKey));

    /// <summary>
    /// Creates a signed JWT with all Liens permissions, suitable for happy-path tests.
    /// </summary>
    public static string CreateFullAccessToken(Guid tenantId, Guid userId)
    {
        var allPermissions = new[]
        {
            LiensPermissions.LienRead,
            LiensPermissions.LienCreate,
            LiensPermissions.LienUpdate,
            LiensPermissions.LienOffer,
            LiensPermissions.LienReadOwn,
            LiensPermissions.LienBrowse,
            LiensPermissions.LienPurchase,
            LiensPermissions.LienReadHeld,
            LiensPermissions.LienService,
            LiensPermissions.LienSettle,
            LiensPermissions.CaseRead,
            LiensPermissions.CaseCreate,
            LiensPermissions.CaseUpdate,
            LiensPermissions.TaskRead,
            LiensPermissions.TaskCreate,
            LiensPermissions.TaskEditOwn,
            LiensPermissions.TaskEditAll,
            LiensPermissions.TaskAssign,
            LiensPermissions.TaskComplete,
            LiensPermissions.TaskCancel,
            LiensPermissions.WorkflowManage,
        };
        return CreateToken(tenantId, userId, allPermissions);
    }

    /// <summary>Creates a signed JWT with explicit permission set.</summary>
    public static string CreateToken(Guid tenantId, Guid userId, string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("sub",        userId.ToString()),
            new("tenant_id",  tenantId.ToString()),
            new("org_id",     SeedHelper.OrgId.ToString()),
            // product_roles claim grants access to SYNQ_LIENS product
            new("product_roles", "SYNQ_LIENS:SYNQLIENS_USER"),
        };

        foreach (var perm in permissions)
            claims.Add(new Claim("permissions", perm));

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer:             Issuer,
            audience:           Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow.AddMinutes(-1),
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Creates an auth header value string: "Bearer {token}".</summary>
    public static string BearerHeader(Guid tenantId, Guid userId)
        => $"Bearer {CreateFullAccessToken(tenantId, userId)}";
}
