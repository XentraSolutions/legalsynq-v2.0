using Xenia.Application.Email.Operations;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// No-DB fallback implementations for email operations services.
/// Registered when no XeniaDb connection string is configured so that
/// ASP.NET Core minimal-API endpoint mapping can always resolve these
/// services at startup (avoids "Body was inferred" pipeline-build crash).
/// All methods throw at runtime — callers receive 500/503 responses,
/// which is acceptable when email operations require a database.
/// </summary>

internal sealed class UnavailableRunQueryService : IRunQueryService
{
    private static InvalidOperationException Unavailable() =>
        new("Email run query is not available without a database connection.");

    public Task<RunPageResult> ListAsync(RunListQuery query, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<RunDetailResult?> GetDetailAsync(Guid tenantId, Guid runId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<RunRetryResult> RetryAsync(Guid tenantId, Guid runId, Guid? actorId, string? correlationId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<RunCancellationResult> CancelAsync(Guid tenantId, Guid runId, Guid? actorId, string? correlationId, CancellationToken ct = default) =>
        throw Unavailable();
}

internal sealed class UnavailableRetentionService : IRetentionService
{
    private static InvalidOperationException Unavailable() =>
        new("Email retention is not available without a database connection.");

    public Task<EmailRetentionRun> ExecuteAsync(Guid tenantId, EmailRetentionMode mode, Guid? actorId, string? correlationId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<IReadOnlyList<EmailRetentionRun>> GetHistoryAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
        throw Unavailable();
}

internal sealed class UnavailableAlertService : IAlertService
{
    private static InvalidOperationException Unavailable() =>
        new("Email alerts are not available without a database connection.");

    public Task<EmailOperationalAlert> OpenOrIncrementAsync(Guid tenantId, EmailAlertType alertType, EmailAlertSeverity severity, string title, string safeDescription, Guid? emailSourceId = null, EmailProviderType? providerType = null, string? correlationId = null, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<AlertPageResult> ListAsync(AlertListQuery query, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<EmailOperationalAlert?> GetAsync(Guid tenantId, Guid alertId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> AcknowledgeAsync(Guid tenantId, Guid alertId, Guid actorId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> ResolveAsync(Guid tenantId, Guid alertId, Guid actorId, string? reason, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> SuppressAsync(Guid tenantId, Guid alertId, Guid actorId, DateTime suppressedUntil, CancellationToken ct = default) =>
        throw Unavailable();

    public Task AutoResolveAsync(Guid tenantId, EmailAlertType alertType, Guid? emailSourceId, string reason, CancellationToken ct = default) =>
        throw Unavailable();
}

internal sealed class UnavailableOperationsSummaryService : IOperationsSummaryService
{
    public Task<OperationsSummaryResult> GetSummaryAsync(OperationsSummaryQuery query, CancellationToken ct = default) =>
        throw new InvalidOperationException("Email operations summary is not available without a database connection.");
}

internal sealed class UnavailableSourceHealthService : ISourceHealthService
{
    private static InvalidOperationException Unavailable() =>
        new("Email source health is not available without a database connection.");

    public Task<IReadOnlyList<SourceHealthSnapshot>> GetAllAsync(Guid tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<SourceHealthSnapshot?> GetAsync(Guid tenantId, Guid sourceId, CancellationToken ct = default) =>
        throw Unavailable();
}

internal sealed class UnavailableProviderHealthService : IProviderHealthService
{
    public Task<IReadOnlyList<ProviderHealthSnapshot>> GetAllAsync(Guid tenantId, CancellationToken ct = default) =>
        throw new InvalidOperationException("Email provider health is not available without a database connection.");
}

internal sealed class UnavailableEmailOperationalSettingsService : IEmailOperationalSettingsService
{
    private static InvalidOperationException Unavailable() =>
        new("Email operational settings are not available without a database connection.");

    public Task<EmailOperationalSettings> GetOrCreateAsync(Guid tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<EmailOperationalSettings> UpdateAsync(Guid tenantId, UpdateOperationalSettingsRequest request, string? updatedBy, CancellationToken ct = default) =>
        throw Unavailable();
}
