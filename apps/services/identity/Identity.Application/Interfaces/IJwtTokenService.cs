using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user, Tenant tenant, IEnumerable<string> roles);
}
