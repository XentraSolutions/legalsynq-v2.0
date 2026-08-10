using Xenia.Domain.Email;

namespace Xenia.Application.Email;

/// <summary>
/// Catalog of supported email provider definitions.
/// Data-driven provider configuration — add a new provider by adding a new entry.
/// No schema changes required for new providers.
/// </summary>
public static class EmailProviderDefinitions
{
    private static readonly IReadOnlyList<EmailProviderDefinitionDto> All =
    [
        new EmailProviderDefinitionDto
        {
            ProviderKey = EmailProviderType.Microsoft365.ToString(),
            DisplayName = "Microsoft 365",
            Category = "Cloud",
            SupportedAuthTypes = [
                EmailAuthType.OAuth2.ToString(),
                EmailAuthType.ClientCredentials.ToString(),
                EmailAuthType.SecretReference.ToString(),
            ],
            DefaultIncomingHost = "outlook.office365.com",
            DefaultPort = 993,
            RequiresTls = true,
            SupportsOAuth = true,
            SupportsUsernamePassword = false,
            ValidationAvailable = true,
            HelpText = "Connect to Microsoft 365 / Exchange Online mailboxes via IMAP or Microsoft Graph API. " +
                       "OAuth2 is strongly recommended. App passwords must be enabled by the M365 admin.",
        },
        new EmailProviderDefinitionDto
        {
            ProviderKey = EmailProviderType.Google.ToString(),
            DisplayName = "Google Workspace / Gmail",
            Category = "Cloud",
            SupportedAuthTypes = [
                EmailAuthType.OAuth2.ToString(),
                EmailAuthType.AppPassword.ToString(),
                EmailAuthType.SecretReference.ToString(),
            ],
            DefaultIncomingHost = "imap.gmail.com",
            DefaultPort = 993,
            RequiresTls = true,
            SupportsOAuth = true,
            SupportsUsernamePassword = false,
            ValidationAvailable = true,
            HelpText = "Connect to Gmail or Google Workspace accounts via IMAP. " +
                       "OAuth2 is required for regular accounts. App passwords may be used when 2-Step Verification is enabled.",
        },
        new EmailProviderDefinitionDto
        {
            ProviderKey = EmailProviderType.Imap.ToString(),
            DisplayName = "IMAP",
            Category = "Protocol",
            SupportedAuthTypes = [
                EmailAuthType.UsernamePassword.ToString(),
                EmailAuthType.AppPassword.ToString(),
                EmailAuthType.SecretReference.ToString(),
            ],
            DefaultIncomingHost = null,
            DefaultPort = 993,
            RequiresTls = true,
            SupportsOAuth = false,
            SupportsUsernamePassword = true,
            ValidationAvailable = true,
            HelpText = "Connect to any IMAP-compatible mail server. " +
                       "TLS (port 993) is required. Plaintext (port 143) is not supported.",
        },
        new EmailProviderDefinitionDto
        {
            ProviderKey = EmailProviderType.Pop3.ToString(),
            DisplayName = "POP3",
            Category = "Protocol",
            SupportedAuthTypes = [
                EmailAuthType.UsernamePassword.ToString(),
                EmailAuthType.AppPassword.ToString(),
                EmailAuthType.SecretReference.ToString(),
            ],
            DefaultIncomingHost = null,
            DefaultPort = 995,
            RequiresTls = true,
            SupportsOAuth = false,
            SupportsUsernamePassword = true,
            ValidationAvailable = true,
            HelpText = "Connect to any POP3-compatible mail server. " +
                       "TLS (port 995) is required. POP3 does not support folders — messages are retrieved from the default inbox.",
        },
        new EmailProviderDefinitionDto
        {
            ProviderKey = EmailProviderType.ExchangeImap.ToString(),
            DisplayName = "Exchange Server (IMAP)",
            Category = "Enterprise",
            SupportedAuthTypes = [
                EmailAuthType.OAuth2.ToString(),
                EmailAuthType.UsernamePassword.ToString(),
                EmailAuthType.SecretReference.ToString(),
            ],
            DefaultIncomingHost = null,
            DefaultPort = 993,
            RequiresTls = true,
            SupportsOAuth = true,
            SupportsUsernamePassword = true,
            ValidationAvailable = true,
            HelpText = "Connect to on-premises or hybrid Microsoft Exchange Server via IMAP. " +
                       "OAuth2 is recommended where supported. UsernamePassword is accepted for legacy on-premises deployments.",
        },
    ];

    /// <summary>Returns all supported provider definitions. Safe for UI consumption.</summary>
    public static IReadOnlyList<EmailProviderDefinitionDto> GetAll() => All;

    /// <summary>Returns a specific provider definition, or null if not found.</summary>
    public static EmailProviderDefinitionDto? Get(string providerKey) =>
        All.FirstOrDefault(p => string.Equals(p.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns true if the auth type is valid for the given provider.</summary>
    public static bool IsAuthTypeSupported(EmailProviderType providerType, EmailAuthType authType)
    {
        var def = Get(providerType.ToString());
        if (def is null) return false;
        return def.SupportedAuthTypes.Contains(authType.ToString(), StringComparer.OrdinalIgnoreCase);
    }
}
