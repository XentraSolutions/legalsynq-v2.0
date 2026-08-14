using Intake.Application.Configuration;
using Intake.Contracts.Sources;

namespace Intake.Application.Sources;

public sealed class IntakeSourceResolver(
    IIntakeSourceRepository repository,
    IIntakeConfigurationService configurationService,
    IIntakeSourceProfileCompatibilityRegistry compatibilityRegistry) : IIntakeSourceResolver
{
    public async Task<ResolvedIntakeSource> ResolveByEmailAddressAsync(
        string emailAddress,
        CancellationToken cancellationToken)
    {
        var normalizedEmailAddress = EmailAddressNormalizer.Normalize(emailAddress);
        var source = await repository.FindByNormalizedEmailAddressAsync(
            normalizedEmailAddress,
            cancellationToken);

        if (source is null)
        {
            throw IntakeConfigurationException.NotFound(
                "INTAKE_SOURCE_NOT_FOUND",
                $"No Intake source is registered for '{normalizedEmailAddress}'.");
        }

        if (!source.IsActive)
        {
            throw IntakeConfigurationException.BadRequest(
                "INTAKE_SOURCE_INACTIVE",
                "The registered Intake source is inactive.");
        }

        try
        {
            await configurationService.ResolveAsync(
                source.TenantId,
                source.ProcessingProfileCode,
                cancellationToken);
        }
        catch (IntakeConfigurationException exception)
            when (exception.Code == "TENANT_INTAKE_DISABLED")
        {
            throw;
        }
        catch (IntakeConfigurationException exception)
        {
            throw IntakeConfigurationException.BadRequest(
                "INTAKE_SOURCE_PROFILE_UNAVAILABLE",
                $"The source processing profile is unavailable: {exception.Code}.");
        }

        compatibilityRegistry.EnsureCompatible(
            source.Purpose,
            source.ProcessingProfileCode);

        return new ResolvedIntakeSource(
            source.Id,
            source.TenantId,
            source.OrgId,
            source.SourceType,
            source.EmailAddress,
            source.NormalizedEmailAddress,
            source.Purpose,
            source.Provider,
            source.ProcessingProfileCode,
            source.ConfigurationVersion,
            DateTimeOffset.UtcNow);
    }
}