using Xenia.Application.Automation;
using Xenia.Application.Automation.Models;
using Xenia.Domain.Automation;

namespace Xenia.Tests.Automation.Infrastructure;

/// <summary>
/// Configurable in-process IAutomationProvider for relational tests.
/// Allows injection of success/failure results without side effects.
/// </summary>
public sealed class FakeAutomationProvider : IAutomationProvider
{
    public string AutomationKey { get; }
    public string Version { get; }
    public bool SupportsCancellation => false;

    private Func<AutomationExecutionRequest, CancellationToken, Task<AutomationExecutionResult>>? _executeFunc;

    public FakeAutomationProvider(
        string automationKey,
        string version = "1.0.0",
        string category = "Test",
        string provider = "FakeProvider")
    {
        AutomationKey = automationKey;
        Version       = version;
        _category     = category;
        _provider     = provider;
    }

    private readonly string _category;
    private readonly string _provider;

    public AutomationManifest GetManifest() => new()
    {
        AutomationKey            = AutomationKey,
        DisplayName              = $"Fake {AutomationKey}",
        Description              = $"Fake provider for {AutomationKey} (tests only)",
        Version                  = Version,
        Category                 = _category,
        Provider                 = _provider,
        Status                   = AutomationLifecycleState.Registered,
        Capabilities             = AutomationCapability.None,
        Dependencies             = Array.Empty<AutomationDependency>(),
        Permissions              = Array.Empty<string>(),
        ConfigurationNamespace   = AutomationKey.ToLowerInvariant(),
        SupportedTriggers        = new[] { AutomationTriggerType.Manual },
        SupportedExecutionModes  = new[] { "default" },
        TenantEnablementSupported = true,
        SchedulingSupported      = false,
        DiagnosticsSupported     = false,
        HealthSupported          = false,
        MinimumPlatformVersion   = "1.0.0",
        MetadataVersion          = 1,
    };

    public IReadOnlyList<AutomationDependency> GetDependencies() =>
        Array.Empty<AutomationDependency>();

    public bool SupportsExecution(AutomationExecutionRequest request) => true;

    public Task<bool> CancelAsync(
        Guid executionId, Guid? tenantId, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<AutomationExecutionResult> ExecuteAsync(
        AutomationExecutionRequest request, CancellationToken ct = default)
    {
        if (_executeFunc is not null)
            return _executeFunc(request, ct);

        return Task.FromResult(new AutomationExecutionResult
        {
            ExecutionId       = Guid.CreateVersion7(),
            AutomationKey     = request.AutomationKey,
            AutomationVersion = Version,
            Status            = AutomationExecutionStatus.Completed,
            StartedAt         = DateTime.UtcNow,
            CompletedAt       = DateTime.UtcNow,
        });
    }

    public FakeAutomationProvider WithExecute(
        Func<AutomationExecutionRequest, CancellationToken, Task<AutomationExecutionResult>> fn)
    {
        _executeFunc = fn;
        return this;
    }

    public FakeAutomationProvider ReturnsFailure(string category = "TEST_FAILURE") =>
        WithExecute((req, _) => Task.FromResult(new AutomationExecutionResult
        {
            ExecutionId       = Guid.CreateVersion7(),
            AutomationKey     = req.AutomationKey,
            AutomationVersion = Version,
            Status            = AutomationExecutionStatus.Failed,
            StartedAt         = DateTime.UtcNow,
            CompletedAt       = DateTime.UtcNow,
            FailureCategory   = category,
            SafeErrorSummary  = "Simulated failure.",
        }));
}
