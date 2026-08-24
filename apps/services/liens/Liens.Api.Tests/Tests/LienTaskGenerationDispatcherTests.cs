using Liens.Application.Interfaces;
using Liens.Application.Services;
using Liens.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LienTaskGenerationDispatcherTests
{
    [Fact]
    public async Task Dispatch_resolves_task_generation_engine_from_a_new_scope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ScopedMarker>();
        services.AddScoped<ILienTaskGenerationEngine, CapturingTaskGenerationEngine>();
        services.AddSingleton<TaskGenerationProbe>();
        services.AddSingleton<ILienTaskGenerationDispatcher, LienTaskGenerationDispatcher>();

        await using var provider = services.BuildServiceProvider();

        Guid callerScopeId;
        var probe = provider.GetRequiredService<TaskGenerationProbe>();

        using (var requestScope = provider.CreateScope())
        {
            callerScopeId = requestScope.ServiceProvider.GetRequiredService<ScopedMarker>().Id;
            var dispatcher = requestScope.ServiceProvider.GetRequiredService<ILienTaskGenerationDispatcher>();

            dispatcher.Dispatch(new TaskGenerationContext(
                TenantId: Guid.CreateVersion7(),
                EventType: TaskGenerationEventType.CaseCreated,
                EntityType: "CASE",
                EntityId: Guid.CreateVersion7(),
                CaseId: Guid.CreateVersion7(),
                LienId: null,
                WorkflowStageId: null,
                ActorUserId: Guid.CreateVersion7()));
        }

        var backgroundScopeId = await probe.ExecutionScopeId.Task.WaitAsync(TimeSpan.FromSeconds(5));
        backgroundScopeId.Should().NotBe(callerScopeId);
    }

    private sealed class ScopedMarker
    {
        public Guid Id { get; } = Guid.CreateVersion7();
    }

    private sealed class TaskGenerationProbe
    {
        public TaskCompletionSource<Guid> ExecutionScopeId { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class CapturingTaskGenerationEngine : ILienTaskGenerationEngine
    {
        private readonly ScopedMarker _marker;
        private readonly TaskGenerationProbe _probe;

        public CapturingTaskGenerationEngine(
            ScopedMarker marker,
            TaskGenerationProbe probe)
        {
            _marker = marker;
            _probe = probe;
        }

        public Task<TaskGenerationResult> TriggerAsync(
            TaskGenerationContext context,
            CancellationToken ct = default)
        {
            _probe.ExecutionScopeId.TrySetResult(_marker.Id);
            return Task.FromResult(new TaskGenerationResult(0, 0));
        }
    }
}
