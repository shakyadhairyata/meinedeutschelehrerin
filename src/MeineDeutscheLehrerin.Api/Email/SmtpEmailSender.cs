using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MeineDeutscheLehrerin.Infrastructure.Identity;

namespace MeineDeutscheLehrerin.Api.Email;

public class EmailSenderOptions
{
    /// <summary>When false (default in dev), emails are logged instead of sent over SMTP.</summary>
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "no-reply@mydeutschteacher.local";
    public string FromName { get; set; } = "MeineDeutscheLehrerin";
}

/// <summary>
/// Identity email sender used by MapIdentityApi for confirmation and password-reset mails.
/// In dev (Enabled=false) it logs the message + link/code so the flow is fully testable
/// without a mail server; in production it sends over SMTP.
/// </summary>
public class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly EmailSenderOptions _opt;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(IOptions<EmailSenderOptions> opt, ILogger<SmtpEmailSender> log)
    {
        _opt = opt.Value;
        _log = log;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Bestätige deine E-Mail-Adresse",
            $"<p>Willkommen bei MeineDeutscheLehrerin!</p><p>Bitte bestätige deine E-Mail-Adresse:</p>" +
            $"<p><a href=\"{confirmationLink}\">E-Mail bestätigen</a></p>");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Passwort zurücksetzen",
            $"<p>Setze dein Passwort über diesen Link zurück:</p><p><a href=\"{resetLink}\">Passwort zurücksetzen</a></p>");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Dein Code zum Zurücksetzen des Passworts",
            $"<p>Dein Code zum Zurücksetzen des Passworts lautet:</p><p style=\"font-size:20px\"><b>{resetCode}</b></p>" +
            "<p>Gib diesen Code zusammen mit deiner E-Mail und dem neuen Passwort ein.</p>");

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        if (!_opt.Enabled || string.IsNullOrWhiteSpace(_opt.Host))
        {
            // Dev mode: surface the message (and the embedded link/code) in the logs.
            _log.LogInformation("[Email:DEV] To={To} | Subject={Subject}\n{Body}", to, subject, htmlBody);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_opt.FromAddress, _opt.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

#pragma warning disable SYSLIB0014 // SmtpClient is fine for this self-contained sender.
        using var client = new SmtpClient(_opt.Host, _opt.Port)
        {
            EnableSsl = _opt.UseSsl,
            Credentials = string.IsNullOrEmpty(_opt.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_opt.Username, _opt.Password)
        };
#pragma warning restore SYSLIB0014
        await client.SendMailAsync(message);
        _log.LogInformation("Sent '{Subject}' email to {To}.", subject, to);
    }
}
