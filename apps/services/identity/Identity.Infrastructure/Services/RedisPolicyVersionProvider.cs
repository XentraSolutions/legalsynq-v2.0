using BuildingBlocks.Authorization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Identity.Infrastructure.Services;

public class RedisPolicyVersionProvider : IPolicyVersionProvider
{
    private const string VersionKey = "legalsynq:policy:version";
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisPolicyVersionProvider> _logger;
    private long _fallbackVersion;

    public RedisPolicyVersionProvider(
        IConnectionMultiplexer redis,
        ILogger<RedisPolicyVersionProvider> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public long CurrentVersion
    {
        get
        {
            try
            {
                var db = _redis.GetDatabase();
                var val = db.StringGet(VersionKey);
                if (val.TryParse(out long version))
                    return version;
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis policy version read failed — using in-memory fallback v{Version}", _fallbackVersion);
                return Interlocked.Read(ref _fallbackVersion);
            }
        }
    }

    public void Increment()
    {
        try
        {
            var db = _redis.GetDatabase();
            var newVersion = db.StringIncrement(VersionKey);
            Interlocked.Exchange(ref _fallbackVersion, newVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis policy version increment failed — incrementing in-memory fallback");
            Interlocked.Increment(ref _fallbackVersion);
        }
    }
}
