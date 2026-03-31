using System.Net;
using System.Net.Mail;
using CareConnect.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CareConnect.Infrastructure.Email;

/// <summary>
/// LSCC-005: Attempts email delivery via SMTP.
/// Configuration keys (all optional — if Host is absent, sends are skipped with a warning):
///   Smtp:Host         — SMTP server hostname
///   Smtp:Port         — port (default 587)
///   Smtp:EnableSsl    — true/false (default true)
///   Smtp:Username     — SMTP auth username
///   Smtp:Password     — SMTP auth password
///   Smtp:FromAddress  — From address (default noreply@legalsynq.com)
///   Smtp:FromName     — From display name (default LegalSynq)
///
/// Throws SmtpException on delivery failure so callers can catch and update
/// the notification record to Failed status. Never silently swallows errors.
/// </summary>
public class SmtpEmailSender : ISmtpEmailSender
{
    private readonly string? _host;
    private readonly int     _port;
    private readonly bool    _enableSsl;
    private readonly string? _username;
    private readonly string? _password;
    private readonly string  _fromAddress;
    private readonly string  _fromName;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _logger      = logger;
        _host        = configuration["Smtp:Host"];
        _port        = int.TryParse(configuration["Smtp:Port"], out var p) ? p : 587;
        _enableSsl   = !string.Equals(configuration["Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);
        _username    = configuration["Smtp:Username"];
        _password    = configuration["Smtp:Password"];
        _fromAddress = configuration["Smtp:FromAddress"] ?? "noreply@legalsynq.com";
        _fromName    = configuration["Smtp:FromName"]    ?? "LegalSynq";
    }

    public async Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_host))
        {
            _logger.LogWarning(
                "SMTP not configured (Smtp:Host is absent). Email to {Recipient} subject '{Subject}' will NOT be sent. " +
                "Notification record remains Pending.",
                toAddress, subject);
            throw new InvalidOperationException("SMTP is not configured — Smtp:Host is missing.");
        }

        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl   = _enableSsl,
            Credentials = (_username is not null && _password is not null)
                ? new NetworkCredential(_username, _password)
                : null,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        using var message = new MailMessage
        {
            From       = new MailAddress(_fromAddress, _fromName),
            Subject    = subject,
            Body       = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(toAddress);

        _logger.LogInformation(
            "Sending email to {Recipient} via {Host}:{Port} subject '{Subject}'",
            toAddress, _host, _port, subject);

        await client.SendMailAsync(message, ct);

        _logger.LogInformation("Email sent successfully to {Recipient}", toAddress);
    }
}
