using Liens.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienTaskGenerationDispatcher : ILienTaskGenerationDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LienTaskGenerationDispatcher> _logger;

    public LienTaskGenerationDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<LienTaskGenerationDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Dispatch(TaskGenerationContext context)
    {
        _ = ExecuteAsync(context);
    }

    private async Task ExecuteAsync(TaskGenerationContext context)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<ILienTaskGenerationEngine>();
            await engine.TriggerAsync(context, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Task generation failed for {EntityType} {EntityId}.",
                context.EntityType,
                context.EntityId);
        }
    }
}
