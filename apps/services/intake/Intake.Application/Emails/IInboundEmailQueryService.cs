using Intake.Contracts.Emails;

namespace Intake.Application.Emails;

public interface IInboundEmailQueryService
{
    Task<PagedInboundEmailResponse> ListAsync(
        Guid tenantId,
        InboundEmailListQuery query,
        CancellationToken cancellationToken);

    Task<InboundEmailDetailResponse?> GetAsync(
        Guid tenantId,
        Guid emailId,
        CancellationToken cancellationToken);

    Task<InboundEmailAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);
}