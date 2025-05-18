using System.Net.Http.Headers;

namespace Zodpovedne.Web.Extensions;

public static class HttpClientFactoryExtensions
{
    public static HttpClient CreateBearerClient(this IHttpClientFactory factory, HttpContext httpContext)
    {
        var client = factory.CreateClient();
        var token = httpContext.Session.GetString("JWTToken");
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Získání IP adresy klienta z HttpContext
        string clientIp = GetClientIpAddress(httpContext);

        // Přidáme IP do hlavičky, pokud není prázdná nebo localhost
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
        string NormalizeIpAddress(string ip)
        {
            if (ip != null && ip.StartsWith("::ffff:"))
            {
                return ip.Substring(7);
            }
            return ip;
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
