// NuGet HtmlSanitizer   //pro bezpeèné èištìní HTML vstupu
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Claims;
using Zodpovedne.Logging;
using Zodpovedne.Logging.Services;

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
                                           // Nastavení pro práci za proxy
                options.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
                });
            });
            // nastavuje, kam se budou ukládat šifrovací klíèe pro ASP.NET Core DataProtection
            //zajišuje šifrování a dešifrování dùležitých dat, jako jsou: Session cookies, Anti - forgery tokeny...
            // Normálnì jsou ukládány do pamìti, øádek viz níže zajistí persistentní uložení, takže i po restartu data ze Session... budou èitelná a platná
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo("/var/www/discussion/keys"));
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

        builder.Services.AddControllers();

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

        // Vytvoøíme instanci FileLoggeru pøímo
        var fileLogger = new FileLogger(builder.Configuration);

        // Pak pøidáme konfiguraci pro ASP.NET Core logging
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddProvider(new CustomFileLoggerProvider(fileLogger));
        });

        // Pøidáme tøídu pro pøeklady
        builder.Services.AddSingleton<Translator>();

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
            sanitizer.AllowedTags.Add("figure"); // Pøidaný pro zarovnání obrázkù
            sanitizer.AllowedTags.Add("figcaption"); // Pøidaný pro popisky obrázkù

            // Povolené HTML atributy
            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.Add("href");
            sanitizer.AllowedAttributes.Add("src");
            sanitizer.AllowedAttributes.Add("alt");
            sanitizer.AllowedAttributes.Add("class"); // Dùležité pro zarovnání
            sanitizer.AllowedAttributes.Add("style"); // Dùležité pro zarovnání
            sanitizer.AllowedAttributes.Add("align"); // Pro starší zpùsob zarovnání
            sanitizer.AllowedAttributes.Add("width");
            sanitizer.AllowedAttributes.Add("height");

            sanitizer.KeepChildNodes = true;

            // Povolené CSS styly
            sanitizer.AllowedCssProperties.Clear();
            sanitizer.AllowedCssProperties.Add("color");
            sanitizer.AllowedCssProperties.Add("font-weight");
            sanitizer.AllowedCssProperties.Add("text-align");
            sanitizer.AllowedCssProperties.Add("margin");
            sanitizer.AllowedCssProperties.Add("margin-left");
            sanitizer.AllowedCssProperties.Add("margin-right");
            sanitizer.AllowedCssProperties.Add("float"); // Dùležité pro zarovnání
            sanitizer.AllowedCssProperties.Add("width");
            sanitizer.AllowedCssProperties.Add("height");
            sanitizer.AllowedCssProperties.Add("display");

            return sanitizer;
        });

        // Konfigurace pro pøístup z proxy
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Specifikujte pøesnì, které hlavièky chcete zpracovávat
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // V produkci omezte dùvìryhodné proxy servery pouze na ty, které známe
            // Pokud nginx bìží na stejném serveru, mùžete dùvìøovat jen localhost
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            // Pøidejte dùvìryhodné proxy servery
            options.KnownProxies.Add(System.Net.IPAddress.Parse("127.0.0.1"));
            options.KnownProxies.Add(System.Net.IPAddress.Parse("::1"));

            // Pokud máte externí proxy server, pøidejte jeho IP adresu
            // options.KnownProxies.Add(System.Net.IPAddress.Parse("IP_ADRESA_PROXY_SERVERU"));
        });

        var app = builder.Build();

        app.UseForwardedHeaders();

        // seznam rout, pro které se ihned vrátí 404 a nepokraèuje se v pipeline
        app.MapShortCircuit(404, "wp-admin", "wp-login", "sitemap.xml", "robots.txt", "/Categories/sitemap.xml", "/Categories/robots.txt");

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
        app.MapControllers();
        app.MapRazorPages();


        var env = app.Services.GetRequiredService<IWebHostEnvironment>();
        string uploadsRoot = Path.Combine(env.WebRootPath, "uploads", "discussions");
        // Smaže všechny doèasné adresáøe (zaèínající "temp_") v adresáøi uploads/discussions
        Zodpovedne.Web.Services.PictureCleaner.CleanTempDirectories(uploadsRoot);
        // Volání metody pro smazání neplatných adresáøù diskuzí
        Zodpovedne.Web.Services.PictureCleaner.CleanupInvalidDiscussionDirectories(uploadsRoot, builder.Configuration["ApiBaseUrl"]);

        app.Run();
    }
}
