namespace BuildingBlocks.Authorization;

public interface IPolicyVersionProvider
{
    long CurrentVersion { get; }
    void Increment();
}
