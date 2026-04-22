using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

// CC2-INT-B06
public class NetworkService : INetworkService
{
    private readonly INetworkRepository  _networks;
    private readonly IProviderRepository _providers;
    private readonly ILogger<NetworkService> _logger;

    public NetworkService(
        INetworkRepository  networks,
        IProviderRepository providers,
        ILogger<NetworkService> logger)
    {
        _networks  = networks;
        _providers = providers;
        _logger    = logger;
    }

    public async Task<List<NetworkSummaryResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var networks = await _networks.GetAllByTenantAsync(tenantId, ct);

        // Load provider counts in parallel — one DB round-trip per network is acceptable
        // given typical network counts (< 50 per tenant).
        var tasks = networks.Select(async n =>
        {
            var detail = await _networks.GetWithProvidersAsync(tenantId, n.Id, ct);
            return ToSummary(n, detail?.NetworkProviders.Count ?? 0);
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    public async Task<NetworkDetailResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var network = await _networks.GetWithProvidersAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Network {id} not found.");

        return ToDetail(network);
    }

    public async Task<NetworkSummaryResponse> CreateAsync(
        Guid tenantId, Guid? userId, CreateNetworkRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);

        if (await _networks.NameExistsAsync(tenantId, request.Name.Trim(), ct: ct))
            throw new ValidationException("Duplicate network name.",
                new() { ["name"] = [$"A network named '{request.Name.Trim()}' already exists."] });

        var network = ProviderNetwork.Create(tenantId, request.Name, request.Description ?? string.Empty);
        await _networks.AddAsync(network, ct);
        await _networks.SaveChangesAsync(ct);

        _logger.LogInformation("Network {NetworkId} created for tenant {TenantId}.", network.Id, tenantId);

        return ToSummary(network, 0);
    }

    public async Task<NetworkSummaryResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid? userId, UpdateNetworkRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);

        var network = await _networks.GetWithProvidersAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Network {id} not found.");

        if (await _networks.NameExistsAsync(tenantId, request.Name.Trim(), excludeId: id, ct: ct))
            throw new ValidationException("Duplicate network name.",
                new() { ["name"] = [$"A network named '{request.Name.Trim()}' already exists."] });

        network.Update(request.Name, request.Description ?? string.Empty);
        await _networks.SaveChangesAsync(ct);

        return ToSummary(network, network.NetworkProviders.Count);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var network = await _networks.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Network {id} not found.");

        network.Delete();
        await _networks.SaveChangesAsync(ct);

        _logger.LogInformation("Network {NetworkId} soft-deleted for tenant {TenantId}.", id, tenantId);
    }

    public async Task AddProviderAsync(
        Guid tenantId, Guid networkId, Guid providerId, Guid? userId, CancellationToken ct = default)
    {
        var network = await _networks.GetByIdAsync(tenantId, networkId, ct)
            ?? throw new NotFoundException($"Network {networkId} not found.");

        var provider = await _providers.GetByIdAsync(tenantId, providerId, ct)
            ?? throw new NotFoundException($"Provider {providerId} not found in tenant.");

        var existing = await _networks.GetMembershipAsync(networkId, providerId, ct);
        if (existing is not null)
        {
            _logger.LogDebug("Provider {ProviderId} already in network {NetworkId} — no-op.", providerId, networkId);
            return;
        }

        var entry = NetworkProvider.Create(tenantId, networkId, providerId);
        await _networks.AddProviderAsync(entry, ct);
        await _networks.SaveChangesAsync(ct);
    }

    public async Task RemoveProviderAsync(
        Guid tenantId, Guid networkId, Guid providerId, CancellationToken ct = default)
    {
        var network = await _networks.GetByIdAsync(tenantId, networkId, ct)
            ?? throw new NotFoundException($"Network {networkId} not found.");

        var entry = await _networks.GetMembershipAsync(networkId, providerId, ct)
            ?? throw new NotFoundException($"Provider {providerId} is not a member of network {networkId}.");

        await _networks.RemoveProviderAsync(entry, ct);
        await _networks.SaveChangesAsync(ct);
    }

    public async Task<List<NetworkProviderMarker>> GetMarkersAsync(
        Guid tenantId, Guid networkId, CancellationToken ct = default)
    {
        _ = await _networks.GetByIdAsync(tenantId, networkId, ct)
            ?? throw new NotFoundException($"Network {networkId} not found.");

        var providers = await _networks.GetNetworkProvidersAsync(tenantId, networkId, ct);

        return providers
            .Where(p => p.Latitude.HasValue && p.Longitude.HasValue)
            .Select(p => new NetworkProviderMarker(
                p.Id,
                p.Name,
                p.OrganizationName,
                p.City,
                p.State,
                p.AddressLine1,
                p.PostalCode,
                p.Email,
                p.Phone,
                p.AcceptingReferrals,
                p.IsActive,
                p.Latitude!.Value,
                p.Longitude!.Value,
                p.GeoPointSource))
            .ToList();
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static NetworkSummaryResponse ToSummary(ProviderNetwork n, int providerCount) =>
        new(n.Id, n.Name, n.Description, providerCount, n.CreatedAtUtc, n.UpdatedAtUtc);

    private static NetworkDetailResponse ToDetail(ProviderNetwork n) =>
        new(
            n.Id,
            n.Name,
            n.Description,
            n.NetworkProviders.Select(np => new NetworkProviderItem(
                np.Provider.Id,
                np.Provider.Name,
                np.Provider.OrganizationName,
                np.Provider.Email,
                np.Provider.Phone,
                np.Provider.City,
                np.Provider.State,
                np.Provider.IsActive,
                np.Provider.AcceptingReferrals)).ToList(),
            n.CreatedAtUtc,
            n.UpdatedAtUtc);

    // ── Validation ────────────────────────────────────────────────────────────

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Validation failed.",
                new() { ["name"] = ["Network name is required."] });
        if (name.Trim().Length > 200)
            throw new ValidationException("Validation failed.",
                new() { ["name"] = ["Network name must be 200 characters or fewer."] });
    }
}
