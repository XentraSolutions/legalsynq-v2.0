using Intake.Contracts.Configuration;

namespace Intake.Application.Configuration;

public interface IIntakeConfigurationService
{
    Task<TenantIntakeConfigurationResponse?> GetConfigurationAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<TenantIntakeConfigurationResponse> UpsertConfigurationAsync(
        Guid tenantId,
        UpsertTenantIntakeConfigurationRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcessingProfileDefinitionResponse>> ListAvailableProfilesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantProcessingProfileResponse>> ListTenantProfilesAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<TenantProcessingProfileResponse> AssignProfileAsync(
        Guid tenantId,
        AssignTenantProcessingProfileRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<TenantProcessingProfileResponse?> GetTenantProfileAsync(
        Guid tenantId,
        string profileCode,
        CancellationToken cancellationToken);

    Task<TenantProcessingProfileResponse> UpdateTenantProfileAsync(
        Guid tenantId,
        string profileCode,
        UpdateTenantProcessingProfileRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<TenantProcessingProfileResponse> UpdateTenantProfileStatusAsync(
        Guid tenantId,
        string profileCode,
        UpdateTenantProcessingProfileStatusRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<ResolvedProcessingConfiguration> ResolveAsync(
        Guid tenantId,
        string? profileCode,
        CancellationToken cancellationToken);
}