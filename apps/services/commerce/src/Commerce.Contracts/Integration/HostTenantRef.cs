namespace Commerce.Contracts.Integration;

/// <summary>
/// Host-platform-neutral reference to a tenant supplied by an external
/// host platform (e.g. LegalSynq Tenant service, a generic SaaS host,
/// or any other system that owns tenant identity).
/// </summary>
/// <param name="HostPlatformKey">
/// Required. Lowercase, normalised key identifying the host platform
/// (e.g. <c>"legalsynq"</c>, <c>"acme-host"</c>).
/// </param>
/// <param name="ExternalTenantId">
/// Required. The host platform's opaque tenant identifier. Commerce
/// stores it verbatim and never validates it against any host service in
/// COM-B08.
/// </param>
/// <param name="DisplayName">
/// Optional. Human-readable label that the host may pass through; not
/// authoritative — the host owns the truth.
/// </param>
/// <param name="MetadataJson">
/// Optional. Free-form JSON the host may attach. Commerce does not
/// interpret this field.
/// </param>
public sealed record HostTenantRef(
    string HostPlatformKey,
    string ExternalTenantId,
    string? DisplayName = null,
    string? MetadataJson = null);
