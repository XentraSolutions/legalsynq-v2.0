namespace BuildingBlocks.Authorization;

/// <summary>
/// Resolves capabilities for a set of product role codes.
/// Capabilities are NOT stored in the JWT — they are resolved per-request
/// from the product_roles claims and cached by the implementation.
/// </summary>
public interface ICapabilityService
{
    /// <summary>
    /// Returns true if any of the given product role codes grants the specified capability.
    /// Platform admin bypass must be checked by the caller before invoking this method.
    /// </summary>
    Task<bool> HasCapabilityAsync(
        IReadOnlyCollection<string> productRoleCodes,
        string capabilityCode,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full set of capability codes granted by the given product role codes.
    /// Result is cached by role code combination.
    /// </summary>
    Task<IReadOnlySet<string>> GetCapabilitiesAsync(
        IReadOnlyCollection<string> productRoleCodes,
        CancellationToken ct = default);
}
