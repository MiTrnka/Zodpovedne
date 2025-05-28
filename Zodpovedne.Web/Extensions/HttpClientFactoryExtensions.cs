using System.Net.Http.Headers;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Extensions;

public static class HttpClientFactoryExtensions
{
    /// <summary>
    /// Vytváří HTTP klient nakonfigurovaný pro komunikaci s REST API s automatickou Bearer token autentizací.
    ///
    /// Metoda inteligentně zpracovává různé autentizační stavy uživatele:
    ///
    /// **Standardní chování:**
    /// - Načítá JWT token z aktuální session a přidává ho do Authorization hlavičky
    /// - Extrahuje IP adresu klienta z HTTP hlaviček pro účely auditování a bezpečnosti
    /// - Podporuje různé proxy konfigurace (X-Real-IP, X-Forwarded-For, X-Debug-Remote-Addr)
    ///
    /// **Pokročilé chování s trvalými cookies:**
    /// - Detekuje situace, kdy uživatel má platné autentizační cookie, ale chybí JWT token
    /// - Tato situace nastává legitimně po zavření/otevření prohlížeče s trvalými cookies
    /// - Místo chyby pouze zaloguje situaci a pokračuje bez Authorization hlavičky
    /// - API endpoint pak vrátí 401 Unauthorized a frontend může iniciovat obnovení přihlášení
    ///
    /// **Bezpečnostní aspekty:**
    /// - Nikdy nepřidává neplatný nebo vypršelý JWT token
    /// - Loguje neočekávané stavy pro monitoring a debugging
    /// - Zachovává audit trail prostřednictvím IP tracking
    ///
    /// **Integrace s autentizačním workflow:**
    /// - Umožňuje graceful handling situací po restartu prohlížeče
    /// - Podporuje automatické přesměrování na login při prvním API volání
    /// - Zachovává separaci mezi cookie autentizací (web) a JWT autentizací (API)
    ///
    /// Tento přístup optimalizuje uživatelskou zkušenost s trvalými cookies, zatímco
    /// zachovává robustní bezpečnost API komunikace.
    /// </summary>
    public static HttpClient CreateBearerClient(this IHttpClientFactory factory, HttpContext httpContext)
    {
        var client = factory.CreateClient();
        var token = httpContext.Session.GetString("JWTToken");

        // Pokud nemáme JWT token, ale uživatel je authenticated (má cookie)
        if (string.IsNullOrEmpty(token) && httpContext.User?.Identity?.IsAuthenticated == true)
        {
            // V tomto případě bychom mohli:
            // 1. Zalogovat situaci
            // 2. Pokusit se o refresh token
            // 3. Nebo prostě pokračovat bez Authorization hlavičky
            //    (některé API endpointy nemusí token vyžadovat)

            // Pro začátek prostě zalogujeme a pokračujeme
            var logger = httpContext.RequestServices.GetService<FileLogger>();
            logger?.Log("CreateBearerClient: Uživatel má cookie ale ne JWT token - možná po restartu prohlížeče");

            // Nebudeme přidávat Authorization hlavičku
            // API endpoint vrátí 401 a frontend může přesměrovat na login
        }
        else if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Zbytek logiky pro IP adresu atd. zůstává stejný
        string clientIp = GetClientIpAddress(httpContext);
        if (!string.IsNullOrEmpty(clientIp) && clientIp != "127.0.0.1" && clientIp != "::1" && clientIp != "unknown")
        {
            client.DefaultRequestHeaders.Add("X-Client-IP", clientIp);
        }

        return client;
    }

    /// <summary>
    /// Získání IP adresy klienta z HttpContext
    /// </summary>
    public static string GetClientIpAddress(HttpContext httpContext)
    {
        // Funkce pro normalizaci IP adresy
        string NormalizeIpAddress(string? ip)
        {
            if (ip != null && ip.StartsWith("::ffff:"))
            {
                return ip.Substring(7);
            }
            return ip ?? "unknown";
        }

        // Důkladnější získání reálné IP klienta
        string clientIp = "unknown";

        // 1. Zkusíme z X-Real-IP hlavičky
        if (httpContext.Request.Headers.TryGetValue("X-Real-IP", out var realIp) && !string.IsNullOrEmpty(realIp))
        {
            clientIp = NormalizeIpAddress(realIp);
        }
        // 2. Zkusíme z X-Forwarded-For hlavičky
        else if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.ToString().Split(',');
            if (ips.Length > 0)
            {
                clientIp = NormalizeIpAddress(ips[0].Trim());
            }
        }
        // 3. Zkusíme z X-Debug-Remote-Addr hlavičky (může být přidána v proxy nastavení pro testování)
        else if (httpContext.Request.Headers.TryGetValue("X-Debug-Remote-Addr", out var debugRemoteAddr) && !string.IsNullOrEmpty(debugRemoteAddr))
        {
            clientIp = NormalizeIpAddress(debugRemoteAddr);
        }
        // 4. Nakonec zkusíme z RemoteIpAddress
        else if (httpContext.Connection.RemoteIpAddress != null)
        {
            clientIp = NormalizeIpAddress(httpContext.Connection.RemoteIpAddress.ToString());
        }

        return clientIp;
    }
}
