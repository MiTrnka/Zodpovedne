using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Zodpovedne.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(5214); // Naslouchá na všech IP adresách, abych mohl pøistupovat z jiných zaøízení (ne jen z Localhost)
            //options.Listen(System.Net.IPAddress.Parse("192.168.0.213"), 5214); // Naslouchá na konkrétní IP adrese
        });

        // Nastavení autentizace pro používání cookie autentizace jako výchozího schématu. Toto se muselo pøidat k tokenùm (autentizace/autorizace pro volání RESTAPI) kvùli tomu, aby fungovala autentizace i pro razor pages.
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
           // Konfigurace cookie autentizace
           .AddCookie(options =>
           {
               // Cesta kam pøesmìrovat, když uživatel není pøihlášen
               options.LoginPath = "/Account/Login";

               // Cesta kam pøesmìrovat pøi odhlášení
               options.LogoutPath = "/Account/Logout";

               // Doba platnosti cookie
               options.ExpireTimeSpan = TimeSpan.FromHours(12);

               // Událost která se spustí pøi pøihlášení uživatele
               // Protože nìkteré claimy z JWT tokenù se nenamapují automaticky, je potøeba je pøidat ruènì
               // Napøíklad ClaimType.Role se automaticky mapuje na Role claim, ale NameIdentifier ne
               options.Events.OnSigningIn = async context =>
               {
                   // Získání identity uživatele z kontextu
                   var identity = (ClaimsIdentity)context.Principal.Identity;

                   // Kontrola jestli už existuje NameIdentifier claim
                   var userId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                   // Pokud NameIdentifier neexistuje
                   if (userId == null)
                   {
                       // Hledání ID uživatele v jiných standardních claimech (sub nebo nameid)
                       var userIdClaim = identity.FindFirst("sub") ??
                                        identity.FindFirst("nameid");

                       // Pokud byl nalezen claim s ID uživatele, pøidá se jako NameIdentifier
                       if (userIdClaim != null)
                       {
                           identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userIdClaim.Value));
                       }
                   }
               };
           });
        // Add services to the container.
        builder.Services.AddRazorPages();

        // Pøidáme služby pro session
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(12);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        // Pøidáme HttpClient pro volání API
        builder.Services.AddHttpClient();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        // Pøidáme middleware pro session
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseSession();
        app.MapRazorPages();

        app.Run();
    }
}
