namespace Reports.Contracts.Adapters;

public interface IIdentityAdapter
{
    Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default);
    Task<string?> GetUserIdFromTokenAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetUserRolesAsync(string userId, CancellationToken ct = default);
}
