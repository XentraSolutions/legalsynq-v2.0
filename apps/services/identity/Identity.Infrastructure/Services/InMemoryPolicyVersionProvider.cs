using BuildingBlocks.Authorization;

namespace Identity.Infrastructure.Services;

public class InMemoryPolicyVersionProvider : IPolicyVersionProvider
{
    private long _version;

    public long CurrentVersion => Interlocked.Read(ref _version);

    public void Increment()
    {
        Interlocked.Increment(ref _version);
    }
}
