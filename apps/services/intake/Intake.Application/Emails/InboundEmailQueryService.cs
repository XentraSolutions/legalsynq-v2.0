using Intake.Contracts.Emails;

namespace Intake.Application.Emails;

public sealed class InboundEmailQueryService(
    IInboundEmailRepository repository) : IInboundEmailQueryService
{
    public Task<PagedInboundEmailResponse> ListAsync(
        Guid tenantId,
        InboundEmailListQuery query,
        CancellationToken cancellationToken) =>
        repository.ListAsync(tenantId, query, cancellationToken);

    public async Task<InboundEmailDetailResponse?> GetAsync(
        Guid tenantId,
        Guid emailId,
        CancellationToken cancellationToken)
    {
        var email = await repository.FindTenantEmailAsync(
            tenantId,
            emailId,
            cancellationToken);
        return email is null ? null : InboundEmailDetailMapper.Map(email);
    }

    public Task<InboundEmailAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken) =>
        repository.GetAnalyticsAsync(tenantId, from, to, cancellationToken);

}