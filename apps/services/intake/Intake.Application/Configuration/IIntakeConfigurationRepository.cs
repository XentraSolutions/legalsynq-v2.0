using Intake.Domain.Configuration;

namespace Intake.Application.Configuration;

public interface IIntakeConfigurationRepository
{
    Task<TenantIntakeConfiguration?> FindTenantConfigurationAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<ProcessingProfileDefinition?> FindDefinitionByCodeAsync(
        string code,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcessingProfileDefinition>> ListActiveDefinitionsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantProcessingProfile>> ListTenantProfilesAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<TenantProcessingProfile?> FindTenantProfileAsync(
        Guid tenantId,
        string code,
        CancellationToken cancellationToken);

    void AddTenantConfiguration(TenantIntakeConfiguration configuration);
    void AddTenantProfile(TenantProcessingProfile profile);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}