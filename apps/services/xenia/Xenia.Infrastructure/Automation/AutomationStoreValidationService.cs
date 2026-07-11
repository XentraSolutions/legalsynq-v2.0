using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xenia.Application.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// Startup validation hosted service that asserts all automation runtime stores
/// are EF-backed (not in-memory) in non-Development environments.
///
/// Runs once at startup, logs the result, and exits.
/// In Production/Staging: throws <see cref="InvalidOperationException"/> on violation.
/// In Development: logs a warning and continues (allows in-memory overrides in tests).
///
/// Registered via <see cref="AutomationDependencyInjection.AddXeniaAutomation"/>.
/// </summary>
internal sealed class AutomationStoreValidationService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AutomationStoreValidationService> _logger;

    public AutomationStoreValidationService(
        IServiceProvider services,
        IHostEnvironment environment,
        ILogger<AutomationStoreValidationService> logger)
    {
        _services    = services;
        _environment = environment;
        _logger      = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var violations = new List<string>();

        ValidateNotInMemory<IAutomationRegistry>(
            typeof(InMemoryAutomationRegistry), "IAutomationRegistry", violations);

        ValidateNotInMemory<IAutomationRuntimeStateStore>(
            typeof(InMemoryAutomationRuntimeStateStore), "IAutomationRuntimeStateStore", violations);

        ValidateNotInMemory<IAutomationDeadLetterStore>(
            typeof(InMemoryAutomationDeadLetterStore), "IAutomationDeadLetterStore", violations);

        if (violations.Count == 0)
        {
            _logger.LogInformation(
                "Automation store validation passed — all stores are EF-backed.");
            return Task.CompletedTask;
        }

        var message =
            $"Automation store validation failed — in-memory implementations detected in " +
            $"{_environment.EnvironmentName} environment: {string.Join(", ", violations)}. " +
            "Replace with EF-backed implementations (XENIA-P1-PROD-V1-T1).";

        if (_environment.IsProduction() || _environment.IsStaging())
        {
            _logger.LogCritical("{Message}", message);
            throw new InvalidOperationException(message);
        }

        _logger.LogWarning("{Message}", message);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ValidateNotInMemory<TInterface>(
        Type inMemoryType, string interfaceName, List<string> violations)
    {
        try
        {
            var impl = _services.GetRequiredService<TInterface>();
            if (impl?.GetType() == inMemoryType)
                violations.Add($"{interfaceName} → {inMemoryType.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not resolve {Interface} for store validation", interfaceName);
        }
    }
}
