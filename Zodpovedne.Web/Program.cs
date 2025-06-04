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

        int expirationInHours = 1000;
        try
        {
            expirationInHours = builder.Configuration.GetValue<int>("ExpirationInHours");
        }
        catch (Exception)
        {
            Console.WriteLine("ExpirationInHours není nastaveno v appsettings.json, pouije se vıchozí hodnota 6 hodin");
        }

        // Podmíneèná konfigurace podle prostøedí
        if (builder.Environment.IsDevelopment())
        {
            // Vıvojové prostøedí - nastavení portù pro lokální vıvoj
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5214); // Lokální vıvojovı port - naslouchá na všech IP adresách, abych mohl pøistupovat z jinıch zaøízení (ne jen z Localhost)
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
            //zajišuje šifrování a dešifrování dùleitıch dat, jako jsou: Session cookies, Anti - forgery tokeny...
            // Normálnì jsou ukládány do pamìti, øádek viz níe zajistí persistentní uloení, take i po restartu data ze Session... budou èitelná a platná
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo("/var/www/discussion/keys"));
        }

        /// <summary>
        /// Konfigurace cookie autentizace s trvalımi cookies a automatickım obnovováním.
        /// Zajišuje, e uivatelé zùstanou pøihlášeni i po zavøení prohlíeèe.
        /// </summary>
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            // Základní cesty
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";

            // KLÍÈOVÉ NASTAVENÍ PRO TRVALÉ COOKIES - pouze ExpireTimeSpan!
            options.ExpireTimeSpan = TimeSpan.FromHours(expirationInHours);
            options.SlidingExpiration = true; // Automatické obnovování cookie

            // KONFIGURACE COOKIE - BEZ Cookie.Expiration!
            options.Cookie.Name = "DiscussionAuth"; // Vlastní název
            options.Cookie.IsEssential = true;      // Cookie je nutné pro fungování
            options.Cookie.HttpOnly = true;         // Bezpeènost - nedostupné pro JavaScript

            // BEZPEÈNOSTNÍ NASTAVENÍ
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;


            // Events pro claims mapping
            options.Events.OnSigningIn = context =>
            {
                var identity = context.Principal?.Identity as ClaimsIdentity;
                var userId = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userId == null)
                {
                    var userIdClaim = identity?.FindFirst("sub") ?? identity?.FindFirst("nameid");
                    if (userIdClaim != null)
                    {
                        identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, userIdClaim.Value));
                    }
                }

                return Task.CompletedTask;
            };
        });
        // Add services to the container.
        builder.Services.AddRazorPages();

        builder.Services.AddControllers();

        // Pøidáme sluby pro session
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
            sanitizer.AllowedTags.Add("figure"); // Pøidanı pro zarovnání obrázkù
            sanitizer.AllowedTags.Add("figcaption"); // Pøidanı pro popisky obrázkù

            // Povolené HTML atributy
            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.Add("href");
            sanitizer.AllowedAttributes.Add("src");
            sanitizer.AllowedAttributes.Add("alt");
            sanitizer.AllowedAttributes.Add("class"); // Dùleité pro zarovnání
            sanitizer.AllowedAttributes.Add("style"); // Dùleité pro zarovnání
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
            sanitizer.AllowedCssProperties.Add("float"); // Dùleité pro zarovnání
            sanitizer.AllowedCssProperties.Add("width");
            sanitizer.AllowedCssProperties.Add("height");
            sanitizer.AllowedCssProperties.Add("display");

            // Povolíme iframe pro YouTube embedy
            sanitizer.AllowedTags.Add("iframe");
            sanitizer.AllowedAttributes.Add("allowfullscreen");
            sanitizer.AllowedAttributes.Add("frameborder");
            sanitizer.AllowedAttributes.Add("src");
            sanitizer.AllowedAttributes.Add("width");
            sanitizer.AllowedAttributes.Add("height");
            sanitizer.AllowedAttributes.Add("class");

            // Povolíme div pro obálku YouTube embedu
            sanitizer.AllowedTags.Add("div");
            sanitizer.AllowedClasses.Add("embed-responsive");
            sanitizer.AllowedClasses.Add("embed-responsive-16by9");
            sanitizer.AllowedClasses.Add("embed-responsive-item");

            // Pøípadné další uiteèné povolení pro CSS u embedding videí
            sanitizer.AllowedCssProperties.Add("position");
            sanitizer.AllowedCssProperties.Add("top");
            sanitizer.AllowedCssProperties.Add("bottom");
            sanitizer.AllowedCssProperties.Add("left");
            sanitizer.AllowedCssProperties.Add("right");
            sanitizer.AllowedCssProperties.Add("padding-top");
            sanitizer.AllowedCssProperties.Add("overflow");

            // Pøípadné další uiteèné atributy pro iframe
            sanitizer.AllowedAttributes.Add("allow"); // Moderní alternativa k allowfullscreen
            sanitizer.AllowedAttributes.Add("loading"); // Pro lazy loading

            return sanitizer;
        });

        // Konfigurace pro pøístup z proxy
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Specifikujte pøesnì, které hlavièky chcete zpracovávat
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // V produkci omezte dùvìryhodné proxy servery pouze na ty, které známe
            // Pokud nginx bìí na stejném serveru, mùete dùvìøovat jen localhost
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
        app.MapShortCircuit(404, "wp-admin", "wp-login");

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
        // Smae všechny doèasné adresáøe (zaèínající "temp_") v adresáøi uploads/discussions
        Zodpovedne.Web.Services.PictureCleaner.CleanTempDirectories(uploadsRoot);
        // Volání metody pro smazání neplatnıch adresáøù diskuzí
        Zodpovedne.Web.Services.PictureCleaner.CleanupInvalidDiscussionDirectories(uploadsRoot, builder.Configuration["ApiBaseUrl"] ?? throw new ArgumentNullException());

        app.Run();
    }
}
