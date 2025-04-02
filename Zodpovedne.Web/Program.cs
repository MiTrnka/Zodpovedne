// NuGet HtmlSanitizer   //pro bezpeèné èištìní HTML vstupu
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using Zodpovedne.Logging;

namespace Zodpovedne.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        int expirationInHours = 6;
        try
        {
            expirationInHours = builder.Configuration.GetValue<int>("ExpirationInHours");
        }
        catch (Exception)
        {
            Console.WriteLine("ExpirationInHours není nastaveno v appsettings.json, použije se výchozí hodnota 6 hodin");
        }

        // Podmíneèná konfigurace podle prostøedí
        if (builder.Environment.IsDevelopment())
        {
            // Vývojové prostøedí - nastavení portù pro lokální vývoj
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5214); // Lokální vývojový port - naslouchá na všech IP adresách, abych mohl pøistupovat z jiných zaøízení (ne jen z Localhost)
                                           //options.Listen(System.Net.IPAddress.Parse("192.168.0.213"), 5214); // Naslouchá na konkrétní IP adrese
            });
        }
        else
        {
            // Produkèní prostøedí - nastavení pro Nginx
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5000); // Port, na kterém bude Web poslouchat
            });
            // nastavuje, kam se budou ukládat šifrovací klíèe pro ASP.NET Core DataProtection
            //zajišuje šifrování a dešifrování dùležitých dat, jako jsou: Session cookies, Anti - forgery tokeny...
            // Normálnì jsou ukládány do pamìti, øádek viz níže zajistí persistentní uložení, takže i po restartu data ze Session... budou èitelná a platná
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo("/var/www/zodpovedne/keys"));
        }

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
               options.ExpireTimeSpan = TimeSpan.FromHours(expirationInHours);

               // Událost která se spustí pøi pøihlášení uživatele
               // Protože nìkteré claimy z JWT tokenù se nenamapují automaticky, je potøeba je pøidat ruènì
               // Napøíklad ClaimType.Role se automaticky mapuje na Role claim, ale NameIdentifier ne
               options.Events.OnSigningIn = context =>
               {
                   // Získání identity uživatele z kontextu
                   var identity = context.Principal?.Identity as ClaimsIdentity;

                   // Kontrola jestli už existuje NameIdentifier claim
                   var userId = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                   // Pokud NameIdentifier neexistuje
                   if (userId == null)
                   {
                       // Hledání ID uživatele v jiných standardních claimech (sub nebo nameid)
                       var userIdClaim = identity?.FindFirst("sub") ??
                                        identity?.FindFirst("nameid");

                       // Pokud byl nalezen claim s ID uživatele, pøidá se jako NameIdentifier
                       if (userIdClaim != null)
                       {
                           identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, userIdClaim.Value));
                       }
                   }

                   return Task.CompletedTask; //Protože se jedná o událost, je potøeba vrátit Task (pøípadnì událost deklarovat s async, pokud bych v ní mìl nìjaký await)
               };
           });
        // Add services to the container.
        builder.Services.AddRazorPages();

        // Pøidáme služby pro session
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(expirationInHours);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        // Pøidáme HttpClient pro volání API
        builder.Services.AddHttpClient();

        // Pøidáme tøídu pro logování
        builder.Services.AddSingleton<FileLogger>();

        // Registrace HTML sanitizeru jako singleton
        builder.Services.AddSingleton<Ganss.Xss.IHtmlSanitizer>(provider => {
            var sanitizer = new Ganss.Xss.HtmlSanitizer();

            // Konfigurace sanitizeru

            // Povolené HTML tagy
            sanitizer.AllowedTags.Clear();
            sanitizer.AllowedTags.Add("p");
            sanitizer.AllowedTags.Add("br");
            sanitizer.AllowedTags.Add("b");
            sanitizer.AllowedTags.Add("strong");
            sanitizer.AllowedTags.Add("i");
            sanitizer.AllowedTags.Add("em");
            sanitizer.AllowedTags.Add("ul");
            sanitizer.AllowedTags.Add("ol");
            sanitizer.AllowedTags.Add("li");
            sanitizer.AllowedTags.Add("h2");
            sanitizer.AllowedTags.Add("h3");
            sanitizer.AllowedTags.Add("h4");
            sanitizer.AllowedTags.Add("a");
            sanitizer.AllowedTags.Add("img");

            // Povolené HTML atributy
            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.Add("href");
            sanitizer.AllowedAttributes.Add("src");
            sanitizer.AllowedAttributes.Add("alt");

            sanitizer.KeepChildNodes = true;

            // Povolené CSS styly
            sanitizer.AllowedCssProperties.Clear();
            sanitizer.AllowedCssProperties.Add("color");
            sanitizer.AllowedCssProperties.Add("font-weight");
            sanitizer.AllowedCssProperties.Add("text-align");

            return sanitizer;
        });


        // Vytvoøíme instanci FileLoggeru pøímo
        var fileLogger = new FileLogger(builder.Configuration);

        // Pak pøidáme konfiguraci pro ASP.NET Core logging
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddProvider(new CustomFileLoggerProvider(fileLogger));
        });

        var app = builder.Build();

        // seznam rout, pro které se ihned vrátí 404 a nepokraèuje se v pipeline
        app.MapShortCircuit(404, "wp-admin","wp-login");

        // Konfigurace HTTP request pipeline pro produkci.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
            //app.UseHttpsRedirection();
        }

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
