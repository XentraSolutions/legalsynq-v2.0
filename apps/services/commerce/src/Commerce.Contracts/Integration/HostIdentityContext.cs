namespace Commerce.Contracts.Integration;

/// <summary>
/// Host-platform-neutral caller-identity contract supplied by a host
/// platform's integration adapter. COM-B08 does NOT introduce real JWT
/// validation, OAuth flows, or LegalSynq Identity calls — this is a
/// pure contract DTO consumed by Commerce-side handlers.
/// </summary>
/// <param name="HostPlatformKey">
/// Required. Identifies which host platform asserted this identity.
/// </param>
/// <param name="ExternalTenantId">
/// Optional. The host's tenant id under which the caller is operating,
/// if applicable.
/// </param>
/// <param name="ExternalUserId">
/// Optional. The host's stable user id.
/// </param>
/// <param name="Subject">
/// Optional. The "sub" claim or equivalent. May equal
/// <see cref="ExternalUserId"/> on most hosts.
/// </param>
/// <param name="Roles">
/// Roles asserted by the host. Empty when none. Commerce does not assign
/// product permissions in COM-B08.
/// </param>
/// <param name="Scopes">
/// Scopes / permissions asserted by the host. Empty when none.
/// </param>
/// <param name="IsAuthenticated">
/// True when the host adapter has produced a verified identity. The
/// no-op local accessor always returns false.
/// </param>
/// <param name="MetadataJson">
/// Optional pass-through metadata, opaque to Commerce.
/// </param>
public sealed record HostIdentityContext(
    string HostPlatformKey,
    string? ExternalTenantId,
    string? ExternalUserId,
    string? Subject,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Scopes,
    bool IsAuthenticated,
    string? MetadataJson)
{
    /// <summary>
    /// Convenience constructor for an unauthenticated/local context.
    /// </summary>
    public static HostIdentityContext Anonymous(string hostPlatformKey)
        => new(
            HostPlatformKey: hostPlatformKey,
            ExternalTenantId: null,
            ExternalUserId: null,
            Subject: null,
            Roles: Array.Empty<string>(),
            Scopes: Array.Empty<string>(),
            IsAuthenticated: false,
            MetadataJson: null);
}
