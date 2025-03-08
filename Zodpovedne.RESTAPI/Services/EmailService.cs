// NuGet balíček MailKit
using MailKit.Net.Smtp;  // Tohle specifikuje MailKit SmtpClient
using MailKit.Security;
using MimeKit;
using System.Net;  // Pro WebUtility.UrlEncode

namespace Zodpovedne.RESTAPI.Services;

// EmailService.cs
public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string email, string nickname, string resetLink);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string email, string nickname, string resetLink)
    {
        try
        {
            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "25");
            var smtpUsername = _configuration["Email:Username"];
            var smtpPassword = _configuration["Email:Password"];
            var useSsl = bool.Parse(_configuration["Email:UseSSL"] ?? "false");
            var fromEmail = _configuration["Email:FromAddress"];
            var fromName = _configuration["Email:FromName"];

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(nickname, email));
            message.Subject = "Obnovení hesla - Mámou zodpovědně";

            var builder = new BodyBuilder
            {
                HtmlBody = $@"
                <h1>Obnovení hesla</h1>
                <p>Dobrý den, {nickname},</p>
                <p>Obdrželi jsme žádost o obnovení hesla pro Váš účet. Klikněte na následující odkaz pro nastavení nového hesla:</p>
                <p><a href='{resetLink}'>Obnovit heslo</a></p>
                <p>Pokud jste o obnovení hesla nežádali, tento e-mail můžete ignorovat.</p>
                <p>S pozdravem,<br>Tým Mámou zodpovědně</p>",

                TextBody = $@"
                OBNOVENÍ HESLA

                Dobrý den, {nickname},

                Obdrželi jsme žádost o obnovení hesla pro Váš účet. Pro nastavení nového hesla navštivte následující odkaz:

                {resetLink}

                Pokud jste o obnovení hesla nežádali, tento e-mail můžete ignorovat.

                S pozdravem,
                Tým Mámou zodpovědně"
            };

            message.Body = builder.ToMessageBody();

            // Zde používáme MailKit.Net.Smtp.SmtpClient, ne System.Net.Mail.SmtpClient
            using var client = new MailKit.Net.Smtp.SmtpClient();

            // Důležité: přidat callback pro validaci certifikátu při lokálním spojení
            if (smtpServer == "localhost" || smtpServer == "127.0.0.1")
            {
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            }

            // Nastavení delšího timeoutu
            client.Timeout = 60000; // 60 sekund

            await client.ConnectAsync(smtpServer, smtpPort, useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

            // Autentizace pouze pokud jsou zadány přihlašovací údaje
            if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
            {
                await client.AuthenticateAsync(smtpUsername, smtpPassword);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chyba při odesílání e-mailu pro obnovení hesla na {Email}", email);
            throw;
        }
    }
}