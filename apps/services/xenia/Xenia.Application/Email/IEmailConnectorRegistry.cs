using Xenia.Domain.Email;

namespace Xenia.Application.Email;

/// <summary>
/// Registry that maps provider types to their connector implementations.
///
/// Connectors are registered at startup. The registry is read-only at runtime.
/// Adding a new provider requires registering a new IEmailSourceConnector —
/// no schema changes are needed.
/// </summary>
public interface IEmailConnectorRegistry
{
    /// <summary>
    /// Returns the connector for the specified provider type.
    /// Throws <see cref="KeyNotFoundException"/> if no connector is registered.
    /// </summary>
    IEmailSourceConnector GetConnector(EmailProviderType providerType);

    /// <summary>Returns all registered connectors.</summary>
    IReadOnlyList<IEmailSourceConnector> GetAllConnectors();

    /// <summary>Returns true if a connector is registered for the given provider type.</summary>
    bool HasConnector(EmailProviderType providerType);
}
