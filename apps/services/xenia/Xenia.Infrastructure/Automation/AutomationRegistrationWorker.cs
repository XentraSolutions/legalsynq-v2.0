using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xenia.Application.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// Hosted service that registers all IAutomationProvider instances into the
/// IAutomationRegistry at startup. Runs once, then exits — not a long-running loop.
///
/// Uses IServiceScopeFactory to resolve scoped providers within the singleton-scoped registry.
/// </summary>
internal sealed class AutomationRegistrationWorker : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAutomationRegistry _registry;
    private readonly ILogger<AutomationRegistrationWorker> _logger;

    public AutomationRegistrationWorker(
        IServiceScopeFactory scopeFactory,
        IAutomationRegistry registry,
        ILogger<AutomationRegistrationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _registry     = registry;
        _logger       = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var providers = scope.ServiceProvider.GetServices<IAutomationProvider>();
        foreach (var provider in providers)
        {
            var result = await _registry.RegisterAsync(provider, cancellationToken);
            if (result.IsSuccess && !result.WasDuplicate)
                _logger.LogInformation("Automation registered: {Key} v{Version}", provider.AutomationKey, provider.Version);
            else if (result.WasDuplicate)
                _logger.LogDebug("Automation already registered: {Key} v{Version}", provider.AutomationKey, provider.Version);
            else
                _logger.LogWarning("Automation registration failed: {Key} — {Error}", provider.AutomationKey, result.ErrorMessage);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
