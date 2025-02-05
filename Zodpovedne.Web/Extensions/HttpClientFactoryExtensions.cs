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

        return client;
    }
}
