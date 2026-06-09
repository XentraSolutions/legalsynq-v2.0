using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Application.Payments.Abstractions;
using Commerce.Contracts.Payments;
using Commerce.Domain.Payments;
using Commerce.Domain.Payments.Enums;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Payments.Services;

/// <summary>
/// Re-applies the side-effects of a previously-stored provider event
/// log row. Only rows in <c>Failed</c>, <c>Received</c>, or <c>Ignored</c>
/// status may be reprocessed; any other status (including
/// <c>Processed</c>) yields <see cref="ProviderEventReprocessNotAllowedException"/>.
/// <c>Ignored</c> rows are eligible because earlier processing may have
/// short-circuited before persistence (e.g. unknown subscription) and a
/// later operator-driven replay is the documented recovery path.
/// Idempotency continues to be enforced by the recording service via
/// the <c>(Provider, ProviderEventId)</c> unique attempt index.
/// </summary>
public sealed class ProviderEventReplayService : IProviderEventReplayService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IPaymentProviderRegistry _registry;
    private readonly IPaymentRecordingService _recording;
    private readonly ISubscriptionReconciliationService _reconciliation;

    public ProviderEventReplayService(
        CommerceDbContext db,
        IClock clock,
        IPaymentProviderRegistry registry,
        IPaymentRecordingService recording,
        ISubscriptionReconciliationService reconciliation)
    {
        _db = db;
        _clock = clock;
        _registry = registry;
        _recording = recording;
        _reconciliation = reconciliation;
    }

    public async Task<ReprocessProviderEventResponse> ReprocessAsync(Guid eventLogId, CancellationToken ct)
    {
        var log = await _db.PaymentProviderEventLogs.FirstOrDefaultAsync(e => e.Id == eventLogId, ct)
            ?? throw new NotFoundException("PaymentProviderEventLog", eventLogId.ToString());

        if (log.ProcessingStatus is not (PaymentProviderEventProcessingStatus.Failed
                                          or PaymentProviderEventProcessingStatus.Received
                                          or PaymentProviderEventProcessingStatus.Ignored))
        {
            throw new ProviderEventReprocessNotAllowedException(eventLogId, log.ProcessingStatus.ToString());
        }

        var providerImpl = _registry.Get(log.Provider);

        NormalizedProviderEvent normalized;
        try
        {
            normalized = providerImpl.TranslateWebhookEvent(log.PayloadJson);
        }
        catch (Exception ex)
        {
            log.MarkFailed("Re-translation failed: " + ex.GetType().Name, _clock.UtcNow);
            await _db.SaveChangesAsync(ct);
            return new ReprocessProviderEventResponse(log.Id, log.ProcessingStatus, log.ErrorMessage);
        }

        try
        {
            var anyApplied = false;
            switch (normalized.Kind)
            {
                case NormalizedProviderEventKind.PaymentIntentSucceeded:
                case NormalizedProviderEventKind.InvoicePaymentSucceeded:
                    var ok = await _recording.RecordFromEventAsync(normalized, succeeded: true, ct);
                    anyApplied |= ok is not null;
                    anyApplied |= await _reconciliation.ReconcileFromEventAsync(normalized, ct);
                    break;
                case NormalizedProviderEventKind.PaymentIntentFailed:
                case NormalizedProviderEventKind.InvoicePaymentFailed:
                    var bad = await _recording.RecordFromEventAsync(normalized, succeeded: false, ct);
                    anyApplied |= bad is not null;
                    anyApplied |= await _reconciliation.ReconcileFromEventAsync(normalized, ct);
                    break;
                case NormalizedProviderEventKind.SubscriptionCreated:
                case NormalizedProviderEventKind.SubscriptionUpdated:
                case NormalizedProviderEventKind.SubscriptionDeleted:
                    anyApplied |= await _reconciliation.ReconcileFromEventAsync(normalized, ct);
                    break;
                default:
                    break;
            }

            if (anyApplied)
                log.MarkProcessed(_clock.UtcNow);
            else
                log.MarkIgnored("Reprocess applied no changes.", _clock.UtcNow);

            await _db.SaveChangesAsync(ct);
            return new ReprocessProviderEventResponse(log.Id, log.ProcessingStatus, null);
        }
        catch (Exception ex)
        {
            log.MarkFailed(ex.GetType().Name + ": " + ex.Message, _clock.UtcNow);
            await _db.SaveChangesAsync(ct);
            return new ReprocessProviderEventResponse(log.Id, log.ProcessingStatus, log.ErrorMessage);
        }
    }
}
