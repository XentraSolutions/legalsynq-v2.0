namespace CareConnect.Application.Interfaces;

/// <summary>
/// Abstraction over an email delivery mechanism.
/// The implementation attempts SMTP delivery and throws on failure.
/// Callers are responsible for catching exceptions and handling retries.
/// </summary>
public interface ISmtpEmailSender
{
    /// <summary>
    /// Sends an HTML email. Throws if delivery fails.
    /// </summary>
    Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default);
}
