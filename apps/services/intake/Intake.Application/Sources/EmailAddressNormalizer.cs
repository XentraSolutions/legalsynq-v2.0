using System.Net.Mail;
using Intake.Application.Configuration;

namespace Intake.Application.Sources;

public static class EmailAddressNormalizer
{
    public static string Normalize(string? emailAddress)
    {
        var value = emailAddress?.Trim() ?? string.Empty;
        if (value.Length == 0 || value.Length > 320 || value.Any(char.IsWhiteSpace))
            throw IntakeConfigurationException.BadRequest(
                "INVALID_EMAIL_ADDRESS",
                "A valid plain email address is required.");

        MailAddress parsed;
        try
        {
            parsed = new MailAddress(value);
        }
        catch (FormatException)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_EMAIL_ADDRESS",
                "A valid plain email address is required.");
        }

        if (!string.Equals(parsed.Address, value, StringComparison.Ordinal))
            throw IntakeConfigurationException.BadRequest(
                "INVALID_EMAIL_ADDRESS",
                "Display names and multiple addresses are not accepted.");

        var at = value.LastIndexOf('@');
        if (at <= 0 || at == value.Length - 1)
            throw IntakeConfigurationException.BadRequest(
                "INVALID_EMAIL_ADDRESS",
                "A valid plain email address is required.");

        // Preserve local-part case and normalize only the domain. This is
        // conservative and does not apply provider-specific alias collapsing.
        return $"{value[..at]}@{value[(at + 1)..].ToLowerInvariant()}";
    }
}