using Xenia.Application.Email;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// In-memory registry mapping provider types to connector implementations.
///
/// Connectors are registered at startup via <see cref="RegisterConnector"/>.
/// To add a new provider: implement <see cref="IEmailSourceConnector"/> and
/// call RegisterConnector — no schema changes required.
/// </summary>
internal sealed class EmailSourceConnectorRegistry : IEmailConnectorRegistry
{
    private readonly Dictionary<EmailProviderType, IEmailSourceConnector> _connectors = new();

    public void RegisterConnector(IEmailSourceConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);
        if (_connectors.ContainsKey(connector.ProviderType))
            throw new InvalidOperationException(
                $"A connector for provider type '{connector.ProviderType}' is already registered. " +
                "Duplicate connector registration is not permitted.");

        _connectors[connector.ProviderType] = connector;
    }

    public IEmailSourceConnector GetConnector(EmailProviderType providerType)
    {
        if (_connectors.TryGetValue(providerType, out var connector))
            return connector;

        throw new KeyNotFoundException(
            $"No email source connector is registered for provider type '{providerType}'. " +
            "Ensure the connector is registered in DependencyInjection.cs.");
    }

    public IReadOnlyList<IEmailSourceConnector> GetAllConnectors() =>
        _connectors.Values.ToList();

    public bool HasConnector(EmailProviderType providerType) =>
        _connectors.ContainsKey(providerType);
}
