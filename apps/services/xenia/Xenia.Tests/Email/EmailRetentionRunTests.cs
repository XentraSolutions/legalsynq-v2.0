using Xenia.Domain.Email;
using Xunit;

namespace Xenia.Tests.Email;

/// <summary>Tests for EmailRetentionRun domain entity lifecycle.</summary>
public sealed class EmailRetentionRunTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_SetsRunningStatus()
    {
        var run = EmailRetentionRun.Create(TenantId, EmailRetentionMode.DryRun, null, null);
        Assert.Equal(EmailRetentionRunStatus.Running, run.Status);
        Assert.NotEqual(Guid.Empty, run.Id);
        Assert.Equal(TenantId, run.TenantId);
        Assert.Equal(EmailRetentionMode.DryRun, run.Mode);
    }

    [Fact]
    public void Create_WithActorAndCorrelation_SetsFields()
    {
        var actorId = Guid.NewGuid();
        var run     = EmailRetentionRun.Create(TenantId, EmailRetentionMode.Execute, actorId, "corr-123");
        Assert.Equal(actorId, run.ActorId);
        Assert.Equal("corr-123", run.CorrelationId);
    }

    [Fact]
    public void RecordProgress_SetsAllCounters()
    {
        var run = EmailRetentionRun.Create(TenantId, EmailRetentionMode.Execute, null, null);
        run.RecordProgress(100, 80, 20, 10, 5, 15, 2);
        Assert.Equal(100, run.MessagesEligible);
        Assert.Equal(80, run.MessagesDeleted);
        Assert.Equal(20, run.BodiesCleared);
        Assert.Equal(10, run.RunsDeleted);
        Assert.Equal(5, run.AlertsDeleted);
        Assert.Equal(15, run.AttachmentReferencesDeleted);
        Assert.Equal(2, run.Failures);
    }

    [Fact]
    public void Complete_TransitionsToCompleted()
    {
        var run = EmailRetentionRun.Create(TenantId, EmailRetentionMode.Execute, null, null);
        run.Complete();
        Assert.Equal(EmailRetentionRunStatus.Completed, run.Status);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public void Fail_TransitionsToFailed()
    {
        var run = EmailRetentionRun.Create(TenantId, EmailRetentionMode.Execute, null, null);
        run.Fail("out of disk space");
        Assert.Equal(EmailRetentionRunStatus.Failed, run.Status);
        Assert.Equal("out of disk space", run.SafeErrorSummary);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public void Cancel_TransitionsToCancelled()
    {
        var run = EmailRetentionRun.Create(TenantId, EmailRetentionMode.DryRun, null, null);
        run.Cancel();
        Assert.Equal(EmailRetentionRunStatus.Cancelled, run.Status);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public void DryRun_Mode_IsPreserved()
    {
        var run = EmailRetentionRun.Create(TenantId, EmailRetentionMode.DryRun, null, null);
        run.RecordProgress(50, 0, 0, 0, 0, 0, 0);
        run.Complete();
        Assert.Equal(EmailRetentionMode.DryRun, run.Mode);
        // In dry-run, deleted counts should be 0
        Assert.Equal(0, run.MessagesDeleted);
        Assert.Equal(50, run.MessagesEligible);
    }
}
