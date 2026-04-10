namespace Identity.Application.Interfaces;

public record EffectiveAccessResult(
    List<string> Products,
    Dictionary<string, List<string>> ProductRoles,
    List<string> ProductRolesFlat,
    List<string> TenantRoles);

public interface IEffectiveAccessService
{
    Task<EffectiveAccessResult> GetEffectiveAccessAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
