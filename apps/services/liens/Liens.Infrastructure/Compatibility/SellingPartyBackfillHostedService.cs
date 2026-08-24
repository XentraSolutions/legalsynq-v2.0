using Liens.Application.Interfaces;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace Liens.Infrastructure.Compatibility;

public sealed class SellingPartyBackfillHostedService : BackgroundService
{
    private static readonly Guid BackfillActorUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SellingPartyCompatibilityOptions _options;
    private readonly ILogger<SellingPartyBackfillHostedService> _logger;

    public SellingPartyBackfillHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<SellingPartyCompatibilityOptions> options,
        ILogger<SellingPartyBackfillHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.BackfillEnabled) return;

        try
        {
            await using var discoveryScope = _scopeFactory.CreateAsyncScope();
            var db = discoveryScope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var tenantIds = await db.Companies.AsNoTracking()
                .Select(c => c.TenantId).Distinct().OrderBy(id => id).ToListAsync(stoppingToken);

            foreach (var tenantId in tenantIds)
            {
                int processed;
                do
                {
                    processed = await RunBatchWithRetryAsync(tenantId, stoppingToken);
                }
                while (processed >= Math.Clamp(_options.BackfillBatchSize, 1, 1000));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selling-party compatibility backfill failed and will remain resumable from its checkpoint.");
        }
    }

    private async Task<int> RunBatchWithRetryAsync(Guid tenantId, CancellationToken ct)
    {
        var maxAttempts = Math.Clamp(_options.BackfillMaxRetries, 1, 10);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var batchScope = _scopeFactory.CreateAsyncScope();
                var service = batchScope.ServiceProvider.GetRequiredService<ISellingPartyCompatibilityService>();
                return await service.RunBackfillBatchAsync(tenantId, BackfillActorUserId, ct);
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                var delayMs = Math.Clamp(_options.BackfillRetryDelayMilliseconds, 50, 5000) * attempt;
                _logger.LogWarning(ex,
                    "Transient selling-party backfill failure for tenant {TenantId}; retry {Attempt}/{MaxAttempts} in {DelayMs}ms.",
                    tenantId, attempt + 1, maxAttempts, delayMs);
                await Task.Delay(delayMs, ct);
            }
        }
    }

    private static bool IsTransient(Exception exception)
        => exception is TimeoutException
           || exception is MySqlException { IsTransient: true }
           || exception.InnerException is MySqlException { IsTransient: true };
}
