using Commerce.Application.Common.Exceptions;
using Commerce.Application.Payments.Abstractions;
using Commerce.Domain.Payments;
using Commerce.Domain.Payments.Enums;
using Commerce.Infrastructure.Payments.Services;
using Commerce.Infrastructure.Subscriptions.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Commerce.Tests.Payments;

public class ProviderEventReplayServiceTests
{
    private static (PaymentTestHost host, ProviderEventReplayService svc) NewHost()
    {
        var host = new PaymentTestHost();
        var recording = new PaymentRecordingService(host.Db, host.Clock);
        var reconciliation = new SubscriptionReconciliationService(host.Db, host.Clock);
        var svc = new ProviderEventReplayService(host.Db, host.Clock, host.Registry, recording, reconciliation);
        return (host, svc);
    }

    private static PaymentProviderEventLog AddLog(
        PaymentTestHost host,
        PaymentProviderEventProcessingStatus status = PaymentProviderEventProcessingStatus.Failed,
        string evtId = "evt_replay_1")
    {
        var log = PaymentProviderEventLog.Receive(
            PaymentProviderType.Stripe, evtId, "payment_intent.succeeded",
            "{\"raw\":true}", host.Clock.UtcNow);
        if (status == PaymentProviderEventProcessingStatus.Failed)
            log.MarkFailed("boom", host.Clock.UtcNow);
        else if (status == PaymentProviderEventProcessingStatus.Processed)
            log.MarkProcessed(host.Clock.UtcNow);
        else if (status == PaymentProviderEventProcessingStatus.Ignored)
            log.MarkIgnored("ig", host.Clock.UtcNow);
        else if (status == PaymentProviderEventProcessingStatus.Duplicate)
            log.MarkDuplicate(host.Clock.UtcNow);

        host.Db.PaymentProviderEventLogs.Add(log);
        host.Db.SaveChanges();
        return log;
    }

    [Fact]
    public async Task Reprocess_unknown_id_throws_NotFound()
    {
        var (host, svc) = NewHost();
        using var _ = host;
        Func<Task> a = () => svc.ReprocessAsync(Guid.NewGuid(), default);
        await a.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Reprocess_already_processed_throws_NotAllowed()
    {
        var (host, svc) = NewHost();
        using var _ = host;
        var log = AddLog(host, PaymentProviderEventProcessingStatus.Processed);
        Func<Task> a = () => svc.ReprocessAsync(log.Id, default);
        await a.Should().ThrowAsync<ProviderEventReprocessNotAllowedException>();
    }

    [Fact]
    public async Task Reprocess_duplicate_throws_NotAllowed()
    {
        var (host, svc) = NewHost();
        using var _ = host;
        var log = AddLog(host, PaymentProviderEventProcessingStatus.Duplicate);
        Func<Task> a = () => svc.ReprocessAsync(log.Id, default);
        await a.Should().ThrowAsync<ProviderEventReprocessNotAllowedException>();
    }

    [Fact]
    public async Task Reprocess_failed_succeeds_when_translator_succeeds()
    {
        var (host, svc) = NewHost();
        using var _ = host;
        var account = host.AddActiveAccount();
        var log = AddLog(host, PaymentProviderEventProcessingStatus.Failed);

        host.Provider.Translator = _ => new NormalizedProviderEvent(
            PaymentProviderType.Stripe, log.ProviderEventId, "payment_intent.succeeded",
            NormalizedProviderEventKind.PaymentIntentSucceeded,
            null, null, null, null, null, null, null, null,
            BillingAccountId: account.Id,
            SubscriptionId: null,
            ProviderPaymentIntentId: "pi_replayed",
            AmountMinor: 1000,
            Currency: "USD");

        var resp = await svc.ReprocessAsync(log.Id, default);
        resp.Status.Should().Be(PaymentProviderEventProcessingStatus.Processed);

        var refreshed = await host.Db.PaymentProviderEventLogs.AsNoTracking()
            .FirstAsync(e => e.Id == log.Id);
        refreshed.ProcessingStatus.Should().Be(PaymentProviderEventProcessingStatus.Processed);
    }

    [Fact]
    public async Task Reprocess_failed_marks_failed_again_when_translator_throws()
    {
        var (host, svc) = NewHost();
        using var _ = host;
        var log = AddLog(host, PaymentProviderEventProcessingStatus.Failed);
        host.Provider.Translator = _ => throw new InvalidOperationException("bad payload");

        var resp = await svc.ReprocessAsync(log.Id, default);
        resp.Status.Should().Be(PaymentProviderEventProcessingStatus.Failed);
        resp.Reason.Should().Contain("Re-translation failed");
    }
}
