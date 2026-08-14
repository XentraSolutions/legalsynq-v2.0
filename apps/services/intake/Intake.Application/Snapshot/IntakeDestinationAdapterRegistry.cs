namespace Intake.Application.Snapshot;

public sealed class IntakeDestinationAdapterRegistry(
    IEnumerable<IIntakeDestinationAdapter> adapters) : IIntakeDestinationAdapterRegistry
{
    private readonly IReadOnlyDictionary<string, IIntakeDestinationAdapter> adapters =
        adapters.ToDictionary(
            adapter => adapter.Descriptor.AdapterCode,
            StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AdapterDescriptor> List() =>
        adapters.Values
            .Select(adapter => adapter.Descriptor)
            .OrderBy(descriptor => descriptor.AdapterCode, StringComparer.Ordinal)
            .ToArray();

    public IIntakeDestinationAdapter GetRequired(string adapterCode) =>
        adapters.TryGetValue(adapterCode, out var adapter)
            ? adapter
            : throw Configuration.IntakeConfigurationException.NotFound(
                Domain.Snapshot.IntakeAdapterFailureCodes.NotConfigured,
                $"The adapter '{adapterCode}' is not registered.");
}