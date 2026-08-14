using Intake.Application.Configuration;
using Intake.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Intake.Infrastructure.Persistence;

public sealed class EfIntakeConfigurationRepository(IntakeDbContext db)
    : IIntakeConfigurationRepository
{
    public Task<TenantIntakeConfiguration?> FindTenantConfigurationAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        db.TenantIntakeConfigurations
            .SingleOrDefaultAsync(configuration => configuration.TenantId == tenantId, cancellationToken);

    public Task<ProcessingProfileDefinition?> FindDefinitionByCodeAsync(
        string code,
        CancellationToken cancellationToken) =>
        db.ProcessingProfileDefinitions
            .SingleOrDefaultAsync(definition => definition.Code == code, cancellationToken);

    public async Task<IReadOnlyList<ProcessingProfileDefinition>> ListActiveDefinitionsAsync(
        CancellationToken cancellationToken) =>
        await db.ProcessingProfileDefinitions
            .AsNoTracking()
            .Where(definition => definition.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TenantProcessingProfile>> ListTenantProfilesAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await db.TenantProcessingProfiles
            .Include(profile => profile.ProcessingProfileDefinition)
            .Where(profile => profile.TenantId == tenantId)
            .ToListAsync(cancellationToken);

    public Task<TenantProcessingProfile?> FindTenantProfileAsync(
        Guid tenantId,
        string code,
        CancellationToken cancellationToken) =>
        db.TenantProcessingProfiles
            .Include(profile => profile.ProcessingProfileDefinition)
            .SingleOrDefaultAsync(
                profile => profile.TenantId == tenantId &&
                           profile.ProcessingProfileDefinition!.Code == code,
                cancellationToken);

    public void AddTenantConfiguration(TenantIntakeConfiguration configuration) =>
        db.TenantIntakeConfigurations.Add(configuration);

    public void AddTenantProfile(TenantProcessingProfile profile) =>
        db.TenantProcessingProfiles.Add(profile);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw IntakeConfigurationException.Conflict(
                "STALE_CONFIGURATION_VERSION",
                "The resource was changed by another request. Reload it and retry with the current configurationVersion.");
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            throw IntakeConfigurationException.Conflict(
                "CONFIGURATION_CONSTRAINT_CONFLICT",
                "The requested configuration conflicts with an existing tenant configuration or default profile.");
        }
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 } ||
        exception.InnerException?.InnerException is MySqlException { Number: 1062 };
}