using BuildingBlocks.Authorization;

namespace CareConnect.Infrastructure.Services;

/// <summary>
/// Static ICapabilityService implementation for CareConnect.
/// Maps product role codes to capability codes without a DB lookup,
/// since CareConnect does not have access to the Identity database.
/// The PlatformAdmin bypass is applied by AuthorizationService before
/// this service is ever called.
/// </summary>
public sealed class CareConnectCapabilityService : ICapabilityService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RoleCapabilities =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [ProductRoleCodes.CareConnectReferrer] = new HashSet<string>
            {
                CapabilityCodes.ReferralCreate,
                CapabilityCodes.ReferralReadOwn,
                CapabilityCodes.ReferralCancel,
                CapabilityCodes.ProviderSearch,
                CapabilityCodes.ProviderMap,
                CapabilityCodes.AppointmentCreate,
                CapabilityCodes.AppointmentReadOwn,
                CapabilityCodes.DashboardRead,
            },
            [ProductRoleCodes.CareConnectReceiver] = new HashSet<string>
            {
                CapabilityCodes.ReferralReadAddressed,
                CapabilityCodes.ReferralAccept,
                CapabilityCodes.ReferralDecline,
                CapabilityCodes.AppointmentCreate,
                CapabilityCodes.AppointmentUpdate,
                CapabilityCodes.AppointmentManage,
                CapabilityCodes.AppointmentReadOwn,
                CapabilityCodes.ScheduleManage,
                CapabilityCodes.ProviderSearch,
                CapabilityCodes.ProviderMap,
                CapabilityCodes.DashboardRead,
            },
        };

    public Task<bool> HasCapabilityAsync(
        IReadOnlyCollection<string> productRoleCodes,
        string capabilityCode,
        CancellationToken ct = default)
    {
        foreach (var roleCode in productRoleCodes)
        {
            if (RoleCapabilities.TryGetValue(roleCode, out var caps) && caps.Contains(capabilityCode))
                return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<IReadOnlySet<string>> GetCapabilitiesAsync(
        IReadOnlyCollection<string> productRoleCodes,
        CancellationToken ct = default)
    {
        var result = new HashSet<string>();
        foreach (var roleCode in productRoleCodes)
        {
            if (RoleCapabilities.TryGetValue(roleCode, out var caps))
                result.UnionWith(caps);
        }
        return Task.FromResult<IReadOnlySet<string>>(result);
    }
}
