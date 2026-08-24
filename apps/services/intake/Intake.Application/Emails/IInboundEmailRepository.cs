using Intake.Contracts.Emails;
using Intake.Domain.Emails;

namespace Intake.Application.Emails;

public sealed record InboundEmailPersistenceResult(Guid EmailId, bool IsDuplicate);

public interface IInboundEmailRepository
{
    Task RecordCaptureFailureAsync(
        InboundEmailCaptureFailure failure,
        CancellationToken cancellationToken);

    Task<InboundEmailPersistenceResult> PersistCaptureAsync(
        InboundEmail email,
        IReadOnlyList<InboundEmailRecipient> recipients,
        IReadOnlyList<InboundEmailAttachmentMetadata> attachments,
        CancellationToken cancellationToken);

    Task<InboundEmail?> FindTenantEmailAsync(
        Guid tenantId,
        Guid emailId,
        CancellationToken cancellationToken);

    Task<InboundEmail?> FindByProviderIdentityAsync(
        Guid tenantId,
        Guid sourceId,
        string provider,
        string providerMessageId,
        CancellationToken cancellationToken);

    Task<InboundEmail?> FindByInternetMessageIdAsync(
        Guid tenantId,
        Guid sourceId,
        string internetMessageId,
        CancellationToken cancellationToken);

    Task<PagedInboundEmailResponse> ListAsync(
        Guid tenantId,
        InboundEmailListQuery query,
        CancellationToken cancellationToken);

    Task<InboundEmailAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);
}