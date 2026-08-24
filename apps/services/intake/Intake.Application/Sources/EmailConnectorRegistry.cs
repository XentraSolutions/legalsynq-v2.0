using System.Text.Json;
using Intake.Application.Configuration;
using Intake.Contracts.Sources;

namespace Intake.Application.Sources;

public sealed class EmailConnectorRegistry : IEmailConnectorRegistry
{
    private static readonly EmailConnectorCapabilities NoOperationalCapabilities = new(
        SupportsPolling: false,
        SupportsWebhook: false,
        SupportsOAuth: false,
        SupportsAttachmentRetrieval: false,
        SupportsMessageIdLookup: false,
        SupportsMailboxFolders: false);

    private readonly IReadOnlyDictionary<string, IIntakeEmailConnector> connectors =
        new Dictionary<string, IIntakeEmailConnector>(StringComparer.Ordinal)
        {
            [IntakeSourceProviders.Microsoft365] = new ConfigurationOnlyEmailConnector(
                IntakeSourceProviders.Microsoft365,
                NoOperationalCapabilities),
            [IntakeSourceProviders.GoogleWorkspace] = new ConfigurationOnlyEmailConnector(
                IntakeSourceProviders.GoogleWorkspace,
                NoOperationalCapabilities),
            [IntakeSourceProviders.Generic] = new ConfigurationOnlyEmailConnector(
                IntakeSourceProviders.Generic,
                NoOperationalCapabilities),
        };

    public IReadOnlyList<EmailConnectorDefinition> Supported { get; } =
    [
        new(
            IntakeSourceProviders.Microsoft365,
            "Microsoft 365",
            ConfigurationOnly: true,
            NoOperationalCapabilities),
        new(
            IntakeSourceProviders.GoogleWorkspace,
            "Google Workspace",
            ConfigurationOnly: true,
            NoOperationalCapabilities),
        new(
            IntakeSourceProviders.Generic,
            "Generic Email",
            ConfigurationOnly: true,
            NoOperationalCapabilities),
    ];

    public IIntakeEmailConnector GetRequired(string providerCode)
    {
        var normalized = providerCode?.Trim().ToUpperInvariant() ?? string.Empty;
        return connectors.TryGetValue(normalized, out var connector)
            ? connector
            : throw IntakeConfigurationException.BadRequest(
                "UNSUPPORTED_EMAIL_PROVIDER",
                $"Unsupported Intake email provider '{providerCode}'.");
    }

    private sealed class ConfigurationOnlyEmailConnector(
        string providerCode,
        EmailConnectorCapabilities capabilities) : IIntakeEmailConnector
    {
        public string ProviderCode { get; } = providerCode;

        public EmailConnectorCapabilities GetCapabilities() => capabilities;

        public ConnectorValidationResult ValidateConfiguration(string? configurationJson)
        {
            if (string.IsNullOrWhiteSpace(configurationJson))
                return new(true, "No provider-specific connector configuration is required.");

            try
            {
                using var document = JsonDocument.Parse(configurationJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return new(false, "ConnectorConfiguration must be a JSON object.");

                if (document.RootElement.EnumerateObject().Any())
                    return new(false, "This configuration-only connector accepts no provider-specific fields.");

                return new(true, "Connector configuration is valid.");
            }
            catch (JsonException)
            {
                return new(false, "ConnectorConfiguration must be valid JSON.");
            }
        }

        public Task<ConnectorTestResult> TestConnectionAsync(
            string? configurationJson,
            string? credentialReference,
            CancellationToken cancellationToken)
        {
            var validation = ValidateConfiguration(configurationJson);
            return Task.FromResult(
                validation.IsValid
                    ? new ConnectorTestResult(
                        IntakeSourceValidationStatuses.Unavailable,
                        "Live mailbox connectivity is not implemented in LSI-B03.")
                    : new ConnectorTestResult(
                        IntakeSourceValidationStatuses.Invalid,
                        validation.Message));
        }
    }
}