// NuGet balíček MailKit - knihovna pro práci s e-maily a SMTP protokolem
using MailKit.Security;  // Obsahuje třídy pro bezpečné připojení k SMTP serveru
using MimeKit;  // Knihovna pro vytváření e-mailových zpráv ve formátu MIME

namespace Zodpovedne.RESTAPI.Services;

// Definice rozhraní, které abstrahuje službu pro odesílání e-mailů
// To umožňuje snadnější testování a výměnu implementace
public interface IEmailService
{
    // Metoda pro odeslání e-mailu s odkazem pro obnovení hesla
    Task SendPasswordResetEmailAsync(string email, string nickname, string resetLink);
}

// Implementace rozhraní IEmailService využívající knihovnu MailKit
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;  // Pro přístup ke konfiguračním hodnotám z appsettings.json
    private readonly ILogger<EmailService> _logger;  // Pro logování chyb a informací

    // Konstruktor s injektovanými závislostmi
    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // Implementace metody pro odeslání e-mailu s odkazem pro obnovení hesla
    public async Task SendPasswordResetEmailAsync(string email, string nickname, string resetLink)
    {
        try
        {
            // Získání SMTP konfigurace z appsettings.json
            var smtpServer = _configuration["Email:SmtpServer"];  // Adresa SMTP serveru (např. localhost)
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "25");  // Port SMTP serveru (výchozí 25)
            var smtpUsername = _configuration["Email:Username"];  // Uživatelské jméno pro autentizaci (může být prázdné)
            var smtpPassword = _configuration["Email:Password"];  // Heslo pro autentizaci (může být prázdné)
            var useSsl = bool.Parse(_configuration["Email:UseSSL"] ?? "false");  // Zda používat SSL/TLS (výchozí false)
            var fromEmail = _configuration["Email:FromAddress"];  // E-mailová adresa odesílatele
            var fromName = _configuration["Email:FromName"];  // Jméno odesílatele (zobrazí se příjemci)

            // Vytvoření nové e-mailové zprávy
            var message = new MimeMessage();

            // Nastavení odesílatele
            message.From.Add(new MailboxAddress(fromName, fromEmail));

            // Nastavení příjemce (používáme přezdívku jako jméno příjemce)
            message.To.Add(new MailboxAddress(nickname, email));

            // Nastavení předmětu e-mailu
            message.Subject = "Obnovení hesla - Mydiscussion";

            // Vytvoření HTML a textové verze těla e-mailu
            var builder = new BodyBuilder
            {
                // HTML verze e-mailu - pro klienty podporující HTML
                HtmlBody = $@"
               <h1>Obnovení hesla</h1>
               <p>Dobrý den, {nickname},</p>
               <p>Obdrželi jsme žádost o obnovení hesla pro Váš účet. Klikněte na následující odkaz pro nastavení nového hesla:</p>
               <p><a href='{resetLink}'>Obnovit heslo</a></p>
               <p>Pokud jste o obnovení hesla nežádali, tento e-mail můžete ignorovat.</p>
               <p>S pozdravem,<br>Tým Mydiscussion</p>",

                // Textová verze e-mailu - pro klienty nepodporující HTML nebo preferující text
                TextBody = $@"
               OBNOVENÍ HESLA

               Dobrý den, {nickname},

               Obdrželi jsme žádost o obnovení hesla pro Váš účet. Pro nastavení nového hesla navštivte následující odkaz:

               {resetLink}

               Pokud jste o obnovení hesla nežádali, tento e-mail můžete ignorovat.

               S pozdravem,
               Tým Mydiscussion"
            };

            // Přiřazení vytvořeného těla k e-mailové zprávě
            message.Body = builder.ToMessageBody();

            // Tyto hlavičky pomohou poskytovatelům e-mailových služeb správně identifikovat zdroj e-mailu a kam mají být zaslány případné odpovědi nebo chybové zprávy
            message.Headers.Add("Return-Path", fromEmail);
            message.Headers.Add("Reply-To", fromEmail);

            // Vytvoření SMTP klienta pro odeslání zprávy
            // Používáme MailKit.Net.Smtp.SmtpClient, ne zastaralý System.Net.Mail.SmtpClient
            using var client = new MailKit.Net.Smtp.SmtpClient();

            // Pro lokální SMTP server ignorujeme neplatné certifikáty
            // To je důležité při vývoji a testování, kdy často nemáme platný SSL certifikát
            if (smtpServer == "localhost" || smtpServer == "127.0.0.1")
            {
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            }

            // Nastavení delšího timeoutu pro případ pomalé sítě nebo serveru
            client.Timeout = 60000; // 60 sekund

            // Připojení k SMTP serveru s volbou šifrování podle konfigurace
            // SecureSocketOptions.StartTls - začíná nešifrovaně a přepne na šifrované (port 587)
            // SecureSocketOptions.None - bez šifrování (port 25)
            await client.ConnectAsync(smtpServer, smtpPort, useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

            // Autentizace na SMTP serveru - pouze pokud jsou zadány přihlašovací údaje
            if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
            {
                await client.AuthenticateAsync(smtpUsername, smtpPassword);
            }

            // Odeslání e-mailové zprávy
            await client.SendAsync(message);

            // Odpojení od SMTP serveru (true znamená čisté odpojení)
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // Logování případné chyby při odesílání e-mailu
            _logger.LogError(ex, "Chyba při odesílání e-mailu pro obnovení hesla na {Email}", email);

            // Propagace výjimky dál, aby ji mohla obsloužit volající metoda
            throw;
        }
    }
}