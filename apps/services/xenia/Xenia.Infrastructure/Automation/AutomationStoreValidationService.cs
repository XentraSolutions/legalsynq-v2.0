using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xenia.Application.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// Startup validation hosted service that asserts all mutable automation runtime stores
/// are EF-backed (not in-memory) in non-Development environments.
///
/// Validates all seven mutable services:
///   · IAutomationRegistry
///   · IAutomationRuntimeStateStore
///   · IAutomationDeadLetterStore
///   · IAutomationScheduler
///   · IAutomationConfigurationService
///   · IAutomationIdempotencyService
///   · IAutomationExecutionService  (scoped — validated via a temporary scope)
///
/// In Production/Staging: throws <see cref="InvalidOperationException"/> on any violation.
/// In Development: logs a warning and continues (allows in-memory overrides in unit tests).
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

        // ── Singleton stores ────────────────────────────────────────────────
        ValidateNotInMemory<IAutomationRegistry>(
            typeof(InMemoryAutomationRegistry),
            "IAutomationRegistry", violations);

        ValidateNotInMemory<IAutomationRuntimeStateStore>(
            typeof(InMemoryAutomationRuntimeStateStore),
            "IAutomationRuntimeStateStore", violations);

        ValidateNotInMemory<IAutomationDeadLetterStore>(
            typeof(InMemoryAutomationDeadLetterStore),
            "IAutomationDeadLetterStore", violations);

        ValidateNotInMemory<IAutomationScheduler>(
            typeof(DefaultAutomationScheduler),
            "IAutomationScheduler", violations);

        ValidateIsEfBacked<IAutomationConfigurationService>(
            typeof(EfAutomationConfigurationService),
            "IAutomationConfigurationService", violations);

        ValidateIsEfBacked<IAutomationIdempotencyService>(
            typeof(EfAutomationIdempotencyService),
            "IAutomationIdempotencyService", violations);

        // ── Scoped store — validated via a temporary scope ──────────────────
        using var scope = _services.CreateScope();
        ValidateScopedNotInMemory<IAutomationExecutionService>(
            scope.ServiceProvider,
            typeof(DefaultAutomationExecutionService),
            "IAutomationExecutionService", violations);

        // ── Log and fail/warn ────────────────────────────────────────────────
        if (violations.Count == 0)
        {
            _logger.LogInformation(
                "Automation store validation passed — all 7 stores are EF-backed.");
            return Task.CompletedTask;
        }

        var message =
            $"Automation store validation failed — in-memory or default implementations " +
            $"detected in {_environment.EnvironmentName} environment: " +
            $"{string.Join(", ", violations)}. " +
            "All mutable automation stores must use EF-backed implementations (XENIA-P1-PROD-V1-T1).";

        if (_environment.IsProduction() || _environment.IsStaging())
        {
            _logger.LogCritical("{Message}", message);
            throw new InvalidOperationException(message);
        }

        _logger.LogWarning("{Message}", message);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Verifies that the resolved singleton is NOT the specified in-memory type.</summary>
    private void ValidateNotInMemory<TInterface>(
        Type inMemoryType, string interfaceName, List<string> violations)
    {
        try
        {
            var impl = _services.GetRequiredService<TInterface>();
            if (impl?.GetType() == inMemoryType)
                violations.Add($"{interfaceName} → {inMemoryType.Name} (in-memory)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not resolve {Interface} for store validation", interfaceName);
            violations.Add($"{interfaceName} → unresolvable");
        }
    }

    /// <summary>
    /// Verifies that the resolved singleton IS the specified EF-backed type.
    /// Used for services that have no in-memory alternative but must be EF-backed.
    /// </summary>
    private void ValidateIsEfBacked<TInterface>(
        Type expectedEfType, string interfaceName, List<string> violations)
    {
        try
        {
            var impl = _services.GetRequiredService<TInterface>();
            if (impl is null)
            {
                violations.Add($"{interfaceName} → null");
                return;
            }

            if (impl.GetType() != expectedEfType)
                violations.Add($"{interfaceName} → {impl.GetType().Name} (expected {expectedEfType.Name})");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not resolve {Interface} for store validation", interfaceName);
            violations.Add($"{interfaceName} → unresolvable");
        }
    }

    /// <summary>Validates a scoped service within the provided scope.</summary>
    private static void ValidateScopedNotInMemory<TInterface>(
        IServiceProvider scopedProvider, Type inMemoryType,
        string interfaceName, List<string> violations)
    {
        try
        {
            var impl = scopedProvider.GetRequiredService<TInterface>();
            if (impl?.GetType() == inMemoryType)
                violations.Add($"{interfaceName} → {inMemoryType.Name} (in-memory, scoped)");
        }
        catch
        {
            violations.Add($"{interfaceName} → unresolvable (scoped)");
        }
    }
}
