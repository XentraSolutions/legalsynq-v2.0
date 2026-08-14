using Intake.Contracts.Sources;

namespace Intake.Application.Sources;

public interface IIntakeSourceService
{
    Task<IReadOnlyList<IntakeSourceResponse>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IntakeSourceResponse?> GetAsync(
        Guid tenantId,
        Guid sourceId,
        CancellationToken cancellationToken);

    Task<IntakeSourceResponse> CreateAsync(
        Guid tenantId,
        CreateIntakeSourceRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IntakeSourceResponse> UpdateAsync(
        Guid tenantId,
        Guid sourceId,
        UpdateIntakeSourceRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IntakeSourceResponse> UpdateStatusAsync(
        Guid tenantId,
        Guid sourceId,
        UpdateIntakeSourceStatusRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<SourceValidationResponse> ValidateAsync(
        Guid tenantId,
        Guid sourceId,
        int? configurationVersion,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<ConnectorTestResponse> TestConnectorAsync(
        Guid tenantId,
        Guid sourceId,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);
}