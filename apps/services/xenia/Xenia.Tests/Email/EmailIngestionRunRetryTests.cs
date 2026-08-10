using Xenia.Domain.Email;
using Xunit;

namespace Xenia.Tests.Email;

/// <summary>Tests for EmailIngestionRun.CreateRetry factory method.</summary>
public sealed class EmailIngestionRunRetryTests
{
    private static readonly Guid TenantId   = Guid.NewGuid();
    private static readonly Guid SourceId   = Guid.NewGuid();
    private static readonly Guid OriginalId = Guid.NewGuid();

    [Fact]
    public void CreateRetry_SetsCorrectFields()
    {
        var actorId = Guid.NewGuid();
        var run     = EmailIngestionRun.CreateRetry(TenantId, SourceId, OriginalId, actorId, "corr-retry");

        Assert.NotEqual(Guid.Empty, run.Id);
        Assert.Equal(TenantId, run.TenantId);
        Assert.Equal(SourceId, run.EmailSourceId);
        Assert.Equal(OriginalId, run.RetryOfRunId);
        Assert.Equal(actorId, run.ActorId);
        Assert.Equal("corr-retry", run.CorrelationId);
        Assert.Equal(IngestionRunStatus.Queued, run.Status);
        Assert.Equal(IngestionRunTriggerType.Manual, run.TriggerType);
    }

    [Fact]
    public void CreateRetry_NullActor_IsAllowed()
    {
        var run = EmailIngestionRun.CreateRetry(TenantId, SourceId, OriginalId, null, null);
        Assert.Null(run.ActorId);
        Assert.Null(run.CorrelationId);
        Assert.Equal(OriginalId, run.RetryOfRunId);
    }

    [Fact]
    public void CreateRetry_HasDifferentIdFromOriginal()
    {
        var run = EmailIngestionRun.CreateRetry(TenantId, SourceId, OriginalId, null, null);
        Assert.NotEqual(OriginalId, run.Id);
    }

    [Fact]
    public void CreateRetry_IsTerminal_ReturnsFalse_WhenQueued()
    {
        var run = EmailIngestionRun.CreateRetry(TenantId, SourceId, OriginalId, null, null);
        Assert.False(run.IsTerminal);
    }
}
