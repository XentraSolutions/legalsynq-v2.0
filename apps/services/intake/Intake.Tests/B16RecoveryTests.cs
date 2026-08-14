using Intake.Application.Operations;
using Intake.Domain.Operations;
using Intake.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Intake.Tests;

public sealed class B16RecoveryTests
{
    [Fact]
    public void Recovery_options_have_bounded_safe_defaults()
    {
        var options = new IntakeRecoveryOptions();

        Assert.True(options.Enabled);
        Assert.Equal(30, options.ScanIntervalSeconds);
        Assert.Equal(10, options.ProcessingStaleAfterMinutes);
        Assert.Equal(100, options.MaxItemsPerScan);
        Assert.Equal(5, options.MaxRecoveryAttempts);
        Assert.Equal(4, options.MaxConcurrentRecoveries);
        Assert.NotEqual(Guid.Empty, options.RecoveryActorId);
    }

    [Fact]
    public void Failure_sanitizer_does_not_persist_exception_details()
    {
        var failure = FailureSanitizer.FromException(
            new InvalidOperationException("patient name and raw document content"));

        Assert.Equal("RECOVERY_FAILED", failure.Code);
        Assert.Equal(IntakeFailureCategories.Unknown, failure.Category);
        Assert.DoesNotContain("patient", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw document", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deterministic_upstream_handler_requires_operator_without_mutating_history()
    {
        var handler = new DeterministicAttentionRecoveryHandler(
            IntakeRecoveryStages.Classification);
        var item = new IntakeRecoveryWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Stage = IntakeRecoveryStages.Classification,
            ObjectId = Guid.NewGuid(),
            DomainStatus = "PROCESSING",
            RecoveryStatus = IntakeRecoveryStatuses.Processing,
        };

        var result = await handler.RecoverAsync(item, "correlation", CancellationToken.None);

        Assert.False(result.Recovered);
        Assert.False(result.Retryable);
        Assert.Equal("UPSTREAM_REPLAY_REQUIRES_OPERATOR", result.FailureCode);
        Assert.Equal(IntakeFailureCategories.Integrity, result.FailureCategory);
        Assert.Equal("PROCESSING", item.DomainStatus);
    }

    [Fact]
    public void Worker_health_is_safe_and_contains_no_payload_fields()
    {
        var state = new RecoveryWorkerState(new IntakeRecoveryOptions());
        state.ScanStarted();
        state.ScanSucceeded(2, 1);
        state.Recovered();

        var health = state.Snapshot();

        Assert.True(health.Enabled);
        Assert.Equal(2, health.ItemsScanned);
        Assert.Equal(1, health.StaleItemsFound);
        Assert.Equal(1, health.RecoveredCount);
        Assert.Null(health.LastFailureCode);
    }

    [Fact]
    public async Task Worker_health_degrades_when_a_scan_fails_before_any_success()
    {
        var options = new IntakeRecoveryOptions();
        var state = new RecoveryWorkerState(options);
        state.ScanStarted();
        state.Failed("RECOVERY_SCAN_FAILED");

        var result = await new RecoveryWorkerHealthCheck(state, options)
            .CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }
}