using Intake.Contracts.Sources;

namespace Intake.Application.Sources;

public sealed record EmailConnectorCapabilities(
    bool SupportsPolling,
    bool SupportsWebhook,
    bool SupportsOAuth,
    bool SupportsAttachmentRetrieval,
    bool SupportsMessageIdLookup,
    bool SupportsMailboxFolders);

public sealed record EmailConnectorDefinition(
    string Code,
    string DisplayName,
    bool ConfigurationOnly,
    EmailConnectorCapabilities Capabilities);

public sealed record ConnectorValidationResult(bool IsValid, string Message);

public sealed record ConnectorTestResult(string Status, string Message);

public interface IIntakeEmailConnector
{
    string ProviderCode { get; }
    EmailConnectorCapabilities GetCapabilities();
    ConnectorValidationResult ValidateConfiguration(string? configurationJson);
    Task<ConnectorTestResult> TestConnectionAsync(
        string? configurationJson,
        string? credentialReference,
        CancellationToken cancellationToken);
}

public interface IEmailConnectorRegistry
{
    IReadOnlyList<EmailConnectorDefinition> Supported { get; }
    IIntakeEmailConnector GetRequired(string providerCode);
}