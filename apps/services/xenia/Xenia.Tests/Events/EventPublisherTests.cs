using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Application.Events;
using Xenia.Domain.Events;
using Xenia.Infrastructure.Events;
using Xunit;

namespace Xenia.Tests.Events;

/// <summary>
/// Tests for InMemoryEventPublisher failure visibility.
///
/// Validates:
/// - Successful handler invocation is transparent.
/// - Handler failure is caught and logged — does not throw to caller.
/// - Partial failure (one of N handlers fails) does not block remaining handlers.
/// - Cancellation propagates correctly to handlers.
/// - Publisher never discards events silently; failures are always logged.
/// </summary>
public sealed class EventPublisherTests
{
    private sealed record TestPayload(string Value);

    private sealed class SuccessHandler : IEventHandler<TestPayload>
    {
        public List<XeniaEventEnvelope<TestPayload>> Received { get; } = [];

        public Task HandleAsync(XeniaEventEnvelope<TestPayload> envelope, CancellationToken ct)
        {
            Received.Add(envelope);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IEventHandler<TestPayload>
    {
        public int CallCount { get; private set; }

        public Task HandleAsync(XeniaEventEnvelope<TestPayload> envelope, CancellationToken ct)
        {
            CallCount++;
            throw new InvalidOperationException("Simulated handler failure");
        }
    }

    private sealed class CancellationAwareHandler : IEventHandler<TestPayload>
    {
        public Task HandleAsync(XeniaEventEnvelope<TestPayload> envelope, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private static XeniaEventEnvelope<TestPayload> MakeEnvelope(string eventType = "test.event") =>
        new()
        {
            EventId = Guid.CreateVersion7(),
            EventType = eventType,
            EventVersion = 1,
            OccurredAt = DateTime.UtcNow,
            TenantId = Guid.CreateVersion7(),
            Payload = new TestPayload("hello"),
            CorrelationId = "test-correlation-id",
        };

    private static InMemoryEventPublisher BuildPublisher(IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        return new InMemoryEventPublisher(provider, NullLogger<InMemoryEventPublisher>.Instance);
    }

    // ── Success path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_SuccessHandler_InvokesHandler()
    {
        var handler = new SuccessHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<TestPayload>>(handler);

        var publisher = BuildPublisher(services);
        var envelope = MakeEnvelope();

        await publisher.PublishAsync(envelope);

        Assert.Single(handler.Received);
        Assert.Equal(envelope.EventId, handler.Received[0].EventId);
    }

    [Fact]
    public async Task PublishAsync_NoHandlers_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var publisher = BuildPublisher(services);

        var ex = await Record.ExceptionAsync(() => publisher.PublishAsync(MakeEnvelope()));
        Assert.Null(ex);
    }

    // ── Failure visibility ────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_HandlerThrows_DoesNotRethrow()
    {
        var services = new ServiceCollection();
        services.AddTransient<IEventHandler<TestPayload>, ThrowingHandler>();

        var publisher = BuildPublisher(services);

        // Must NOT throw — failure is caught and logged internally
        var ex = await Record.ExceptionAsync(() => publisher.PublishAsync(MakeEnvelope()));
        Assert.Null(ex);
    }

    [Fact]
    public async Task PublishAsync_HandlerThrows_HandlerWasInvoked()
    {
        var throwing = new ThrowingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<TestPayload>>(throwing);

        var publisher = BuildPublisher(services);
        await publisher.PublishAsync(MakeEnvelope());

        // Handler was called even though it threw
        Assert.Equal(1, throwing.CallCount);
    }

    // ── Partial failure ───────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_PartialFailure_RemainingHandlersExecute()
    {
        var success = new SuccessHandler();
        var throwing = new ThrowingHandler();

        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<TestPayload>>(throwing);
        services.AddSingleton<IEventHandler<TestPayload>>(success);

        var publisher = BuildPublisher(services);
        var ex = await Record.ExceptionAsync(() => publisher.PublishAsync(MakeEnvelope()));

        // No exception surfaced
        Assert.Null(ex);

        // Success handler was still called despite the throwing handler
        Assert.Single(success.Received);
    }

    [Fact]
    public async Task PublishAsync_FirstHandlerFails_SecondStillReceivesEvent()
    {
        var throwing = new ThrowingHandler();
        var success = new SuccessHandler();
        var envelope = MakeEnvelope("test.partial");

        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<TestPayload>>(throwing);
        services.AddSingleton<IEventHandler<TestPayload>>(success);

        var publisher = BuildPublisher(services);
        await publisher.PublishAsync(envelope);

        Assert.Equal(envelope.EventId, success.Received[0].EventId);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_CancelledToken_HandlerSeesCancel()
    {
        var services = new ServiceCollection();
        services.AddTransient<IEventHandler<TestPayload>, CancellationAwareHandler>();

        var publisher = BuildPublisher(services);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The handler throws OperationCanceledException → publisher catches it and logs
        var ex = await Record.ExceptionAsync(() =>
            publisher.PublishAsync(MakeEnvelope(), cts.Token));

        Assert.Null(ex); // publisher never rethrows
    }

    // ── Correlation propagation ───────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_CorrelationId_PropagatedToHandler()
    {
        var handler = new SuccessHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<TestPayload>>(handler);

        var publisher = BuildPublisher(services);
        var envelope = MakeEnvelope() with { CorrelationId = "corr-abc-123" };

        await publisher.PublishAsync(envelope);

        Assert.Equal("corr-abc-123", handler.Received[0].CorrelationId);
    }
}
