using BuildingBlocks.Domain;

namespace CareConnect.Domain;

// CC2-INT-B06: Join entity linking a ProviderNetwork to a Provider.
public class NetworkProvider : AuditableEntity
{
    public Guid Id              { get; private set; }
    public Guid TenantId        { get; private set; }
    public Guid ProviderNetworkId { get; private set; }
    public Guid ProviderId      { get; private set; }
    public Guid FacilityId      { get; private set; }
    public bool IsActive        { get; private set; }
    public bool AcceptingReferrals { get; private set; }

    public ProviderNetwork Network  { get; private set; } = null!;
    public Provider        Provider { get; private set; } = null!;
    public Facility        Facility { get; private set; } = null!;

    private NetworkProvider() { }

    public static NetworkProvider Create(
        Guid tenantId,
        Guid networkId,
        Guid providerId,
        Guid facilityId,
        bool isActive,
        bool acceptingReferrals)
    {
        return new NetworkProvider
        {
            Id               = Guid.CreateVersion7(),
            TenantId         = tenantId,
            ProviderNetworkId = networkId,
            ProviderId       = providerId,
            FacilityId       = facilityId,
            IsActive         = isActive,
            AcceptingReferrals = acceptingReferrals,
            CreatedAtUtc     = DateTime.UtcNow,
            UpdatedAtUtc     = DateTime.UtcNow,
        };
    }

    public void UpdateStatus(bool isActive, bool acceptingReferrals)
    {
        IsActive = isActive;
        AcceptingReferrals = acceptingReferrals;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
