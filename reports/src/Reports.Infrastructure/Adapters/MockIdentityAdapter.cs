using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;

namespace Reports.Infrastructure.Adapters;

public sealed class MockIdentityAdapter : IIdentityAdapter
{
    private readonly ILogger<MockIdentityAdapter> _log;

    public MockIdentityAdapter(ILogger<MockIdentityAdapter> log) => _log = log;

    public Task<bool> ValidateTokenAsync(string token, CancellationToken ct)
    {
        _log.LogDebug("MockIdentityAdapter: ValidateToken called");
        return Task.FromResult(!string.IsNullOrWhiteSpace(token));
    }

    public Task<string?> GetUserIdFromTokenAsync(string token, CancellationToken ct)
    {
        _log.LogDebug("MockIdentityAdapter: GetUserIdFromToken called");
        return Task.FromResult<string?>("mock-user-id");
    }

    public Task<IReadOnlyList<string>> GetUserRolesAsync(string userId, CancellationToken ct)
    {
        _log.LogDebug("MockIdentityAdapter: GetUserRoles called for {UserId}", userId);
        return Task.FromResult<IReadOnlyList<string>>(new[] { "reports-viewer", "reports-executor" });
    }
}
