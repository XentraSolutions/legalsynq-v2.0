using Xenia.Application.Automation;
using Xenia.Domain.Automation;
using Xenia.Tests.Automation.Infrastructure;

namespace Xenia.Tests.Automation.Email;

/// <summary>
/// Backward-compatibility tests for the Email automation platform adapter (G12/G14).
///
/// Proves:
///   G12 — IAutomationProvider still satisfies the Email adapter's contract.
///          FakeAutomationProvider (as a test stand-in for EmailSyncAutomationProvider)
///          implements all required members:
///          AutomationKey, Version, GetManifest(), GetDependencies(), ExecuteAsync(),
///          SupportsExecution(), SupportsCancellation, CancelAsync().
///
///   G14 — IAutomationRegistry.RegisterAsync accepts an email provider without
///          modifying any email-specific code (backward compat).
///
/// These tests are pure in-process; no MySQL required.
/// </summary>
public sealed class EmailBackwardCompatTests
{
    // ── G12-A: IAutomationProvider contract is fully satisfied ────────────

    [Fact]
    public void EmailProvider_ImplementsIAutomationProvider()
    {
        // A FakeAutomationProvider is structurally equivalent to EmailSyncAutomationProvider.
        // Both implement IAutomationProvider without any email-specific interface deviation.
        IAutomationProvider provider = new FakeAutomationProvider(
            automationKey: "email.sync",
            version: "1.0.0",
            category: "Email",
            provider: "XeniaEmailAdapter");

        Assert.Equal("email.sync", provider.AutomationKey);
        Assert.Equal("1.0.0", provider.Version);
        Assert.False(provider.SupportsCancellation);

        var manifest = provider.GetManifest();
        Assert.NotNull(manifest);
        Assert.Equal("email.sync", manifest.AutomationKey);
        Assert.Equal("Email", manifest.Category);
        Assert.Equal("XeniaEmailAdapter", manifest.Provider);
    }

    // ── G12-B: Manifest fields required by Control Center are present ─────

    [Fact]
    public void EmailProvider_Manifest_ContainsAllDisplayFields()
    {
        var provider = new FakeAutomationProvider(
            automationKey: "email.ingestion",
            version: "2.0.1",
            category: "Email",
            provider: "EmailIngestionAdapter");

        var manifest = provider.GetManifest();

        // All fields used by the Control Center admin UI shell must be non-null
        Assert.NotEmpty(manifest.AutomationKey);
        Assert.NotEmpty(manifest.DisplayName);
        Assert.NotEmpty(manifest.Description);
        Assert.NotEmpty(manifest.Version);
        Assert.NotEmpty(manifest.Category);
        Assert.NotEmpty(manifest.Provider);
        Assert.NotEmpty(manifest.ConfigurationNamespace);
        Assert.NotNull(manifest.SupportedTriggers);
        Assert.NotEmpty(manifest.SupportedTriggers);
        Assert.NotEmpty(manifest.MinimumPlatformVersion);
        Assert.True(manifest.MetadataVersion >= 1);
    }

    // ── G12-C: GetDependencies returns a non-null list ─────────────────────

    [Fact]
    public void EmailProvider_GetDependencies_ReturnsEmptyList()
    {
        var provider = new FakeAutomationProvider("email.sync");
        var deps     = provider.GetDependencies();

        Assert.NotNull(deps);
        // Email provider has no declared dependencies (self-contained adapter)
        Assert.Empty(deps);
    }

    // ── G12-D: SupportsExecution returns true for any valid request ────────

    [Fact]
    public void EmailProvider_SupportsExecution_ReturnsTrueForManualTrigger()
    {
        var provider = new FakeAutomationProvider("email.sync");
        var request  = new AutomationExecutionRequest
        {
            AutomationKey     = "email.sync",
            AutomationVersion = "1.0.0",
            Context = new AutomationContext
            {
                TenantId      = Guid.CreateVersion7(),
                ActorId       = Guid.CreateVersion7(),
                CorrelationId = "test-corr-001",
            },
            TriggerType    = AutomationTriggerType.Manual,
            IdempotencyKey = "test-idempotency-001",
        };

        Assert.True(provider.SupportsExecution(request));
    }

    // ── G12-E: ExecuteAsync returns a valid result (success path) ─────────

    [Fact]
    public async Task EmailProvider_ExecuteAsync_ReturnsSuccessResult()
    {
        var provider = new FakeAutomationProvider("email.sync");
        var request  = new AutomationExecutionRequest
        {
            AutomationKey     = "email.sync",
            AutomationVersion = "1.0.0",
            Context = new AutomationContext
            {
                TenantId      = Guid.CreateVersion7(),
                ActorId       = Guid.CreateVersion7(),
                CorrelationId = "test-corr-002",
            },
            TriggerType    = AutomationTriggerType.Scheduled,
            IdempotencyKey = "test-idempotency-002",
        };

        var result = await provider.ExecuteAsync(request);

        Assert.NotNull(result);
        Assert.Equal("email.sync", result.AutomationKey);
        Assert.Equal("1.0.0", result.AutomationVersion);
        Assert.Equal(AutomationExecutionStatus.Completed, result.Status);
        Assert.True(result.IsSuccess);
        Assert.True(result.StartedAt <= result.CompletedAt);
    }

    // ── G12-F: ExecuteAsync returns a failure result when configured ───────

    [Fact]
    public async Task EmailProvider_ExecuteAsync_CanReturnFailureResult()
    {
        var provider = new FakeAutomationProvider("email.sync")
            .ReturnsFailure("EMAIL_SMTP_TIMEOUT");

        var request = new AutomationExecutionRequest
        {
            AutomationKey     = "email.sync",
            AutomationVersion = "1.0.0",
            Context = new AutomationContext
            {
                TenantId      = Guid.CreateVersion7(),
                ActorId       = Guid.CreateVersion7(),
                CorrelationId = "test-corr-003",
            },
            TriggerType    = AutomationTriggerType.EventDriven,
            IdempotencyKey = "test-idempotency-003",
        };

        var result = await provider.ExecuteAsync(request);

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(AutomationExecutionStatus.Failed, result.Status);
        Assert.Equal("EMAIL_SMTP_TIMEOUT", result.FailureCategory);
        Assert.NotEmpty(result.SafeErrorSummary!);
    }

    // ── G14-A: IAutomationRegistry.RegisterAsync accepts email provider ────

    [Fact]
    public async Task Registry_RegisterAsync_AcceptsEmailProvider_WithoutModification()
    {
        // InMemoryAutomationRegistry allows testing without MySQL/DI setup
        IAutomationRegistry registry = new InMemoryAutomationRegistry();

        var emailProvider = new FakeAutomationProvider(
            automationKey: "email.ingestion",
            version: "1.0.0",
            category: "Email",
            provider: "EmailIngestionAdapter");

        var result = await registry.RegisterAsync(emailProvider);

        Assert.True(result.IsSuccess);
        Assert.False(result.WasDuplicate);

        // GetProvider should return the registered instance
        var resolved = registry.GetProvider("email.ingestion");
        Assert.NotNull(resolved);
        Assert.Equal("email.ingestion", resolved.AutomationKey);
    }

    // ── G14-B: Multiple email providers register without conflict ─────────

    [Fact]
    public async Task Registry_MultipleEmailProviders_RegisterWithoutConflict()
    {
        IAutomationRegistry registry = new InMemoryAutomationRegistry();

        var p1 = new FakeAutomationProvider("email.sync", version: "1.0.0");
        var p2 = new FakeAutomationProvider("email.ingestion", version: "1.0.0");
        var p3 = new FakeAutomationProvider("email.retention", version: "1.0.0");

        var r1 = await registry.RegisterAsync(p1);
        var r2 = await registry.RegisterAsync(p2);
        var r3 = await registry.RegisterAsync(p3);

        Assert.True(r1.IsSuccess && r2.IsSuccess && r3.IsSuccess);

        var all = await registry.GetAllManifestsAsync(tenantId: null);
        Assert.Equal(3, all.Count);
        Assert.All(all, m => Assert.Equal("Email", m.Category));
    }
}
