using System.Diagnostics;

namespace BuildingBlocks.Authorization;

public class PolicyMetrics
{
    private long _evaluationCount;
    private long _cacheHits;
    private long _cacheMisses;
    private long _cacheErrors;
    private long _versionReadCount;
    private long _totalEvaluationMs;
    private long _totalCacheReadMs;
    private long _totalVersionReadMs;

    public long EvaluationCount => Interlocked.Read(ref _evaluationCount);
    public long CacheHits => Interlocked.Read(ref _cacheHits);
    public long CacheMisses => Interlocked.Read(ref _cacheMisses);
    public long CacheErrors => Interlocked.Read(ref _cacheErrors);
    public long VersionReadCount => Interlocked.Read(ref _versionReadCount);
    public long TotalEvaluationMs => Interlocked.Read(ref _totalEvaluationMs);
    public long TotalCacheReadMs => Interlocked.Read(ref _totalCacheReadMs);
    public long TotalVersionReadMs => Interlocked.Read(ref _totalVersionReadMs);

    public double AverageEvaluationMs => EvaluationCount > 0 ? (double)TotalEvaluationMs / EvaluationCount : 0;
    public double AverageCacheReadMs => (CacheHits + CacheMisses) > 0 ? (double)TotalCacheReadMs / (CacheHits + CacheMisses) : 0;
    public double CacheHitRate => (CacheHits + CacheMisses) > 0 ? (double)CacheHits / (CacheHits + CacheMisses) * 100 : 0;

    public void RecordEvaluation(long elapsedMs)
    {
        Interlocked.Increment(ref _evaluationCount);
        Interlocked.Add(ref _totalEvaluationMs, elapsedMs);
    }

    public void RecordCacheHit(long elapsedMs)
    {
        Interlocked.Increment(ref _cacheHits);
        Interlocked.Add(ref _totalCacheReadMs, elapsedMs);
    }

    public void RecordCacheMiss(long elapsedMs)
    {
        Interlocked.Increment(ref _cacheMisses);
        Interlocked.Add(ref _totalCacheReadMs, elapsedMs);
    }

    public void RecordCacheError()
    {
        Interlocked.Increment(ref _cacheErrors);
    }

    public void RecordVersionRead(long elapsedMs)
    {
        Interlocked.Increment(ref _versionReadCount);
        Interlocked.Add(ref _totalVersionReadMs, elapsedMs);
    }

    public PolicyMetricsSnapshot GetSnapshot() => new()
    {
        EvaluationCount = EvaluationCount,
        CacheHits = CacheHits,
        CacheMisses = CacheMisses,
        CacheErrors = CacheErrors,
        CacheHitRate = CacheHitRate,
        AverageEvaluationMs = AverageEvaluationMs,
        AverageCacheReadMs = AverageCacheReadMs,
        VersionReadCount = VersionReadCount,
    };
}

public class PolicyMetricsSnapshot
{
    public long EvaluationCount { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public long CacheErrors { get; set; }
    public double CacheHitRate { get; set; }
    public double AverageEvaluationMs { get; set; }
    public double AverageCacheReadMs { get; set; }
    public long VersionReadCount { get; set; }
}
