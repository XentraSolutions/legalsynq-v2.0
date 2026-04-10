namespace Identity.Application.Interfaces;

public record EffectiveProductEntry(string ProductCode, string Source, Guid? GroupId = null, string? GroupName = null);
public record EffectiveRoleEntry(string RoleCode, string? ProductCode, string Source, Guid? GroupId = null, string? GroupName = null);

public record EffectiveAccessResult(
    List<string> Products,
    Dictionary<string, List<string>> ProductRoles,
    List<string> ProductRolesFlat,
    List<string> TenantRoles,
    List<EffectiveProductEntry> ProductSources,
    List<EffectiveRoleEntry> RoleSources);

public interface IEffectiveAccessService
{
    Task<EffectiveAccessResult> GetEffectiveAccessAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
