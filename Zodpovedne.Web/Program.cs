// NuGet HtmlSanitizer   //pro bezpe�n� �i�t�n� HTML vstupu
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
            Console.WriteLine("ExpirationInHours nen� nastaveno v appsettings.json, pou�ije se v�choz� hodnota 6 hodin");
        }

        // Podm�ne�n� konfigurace podle prost�ed�
        if (builder.Environment.IsDevelopment())
        {
            // V�vojov� prost�ed� - nastaven� port� pro lok�ln� v�voj
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5214); // Lok�ln� v�vojov� port - naslouch� na v�ech IP adres�ch, abych mohl p�istupovat z jin�ch za��zen� (ne jen z Localhost)
                                           //options.Listen(System.Net.IPAddress.Parse("192.168.0.213"), 5214); // Naslouch� na konkr�tn� IP adrese
            });
        }
        else
        {
            // Produk�n� prost�ed� - nastaven� pro Nginx
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5000); // Port, na kter�m bude Web poslouchat
                                           // Nastaven� pro pr�ci za proxy
                options.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
                });
            });
            // nastavuje, kam se budou ukl�dat �ifrovac� kl��e pro ASP.NET Core DataProtection
            //zaji��uje �ifrov�n� a de�ifrov�n� d�le�it�ch dat, jako jsou: Session cookies, Anti - forgery tokeny...
            // Norm�ln� jsou ukl�d�ny do pam�ti, ��dek viz n�e zajist� persistentn� ulo�en�, tak�e i po restartu data ze Session... budou �iteln� a platn�
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo("/var/www/discussion/keys"));
        }

        // Nastaven� autentizace pro pou��v�n� cookie autentizace jako v�choz�ho sch�matu. Toto se muselo p�idat k token�m (autentizace/autorizace pro vol�n� RESTAPI) kv�li tomu, aby fungovala autentizace i pro razor pages.
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
           // Konfigurace cookie autentizace
           .AddCookie(options =>
           {
               // Cesta kam p�esm�rovat, kdy� u�ivatel nen� p�ihl�en
               options.LoginPath = "/Account/Login";

               // Cesta kam p�esm�rovat p�i odhl�en�
               options.LogoutPath = "/Account/Logout";

               // Doba platnosti cookie
               options.ExpireTimeSpan = TimeSpan.FromHours(expirationInHours);

               // Ud�lost kter� se spust� p�i p�ihl�en� u�ivatele
               // Proto�e n�kter� claimy z JWT token� se nenamapuj� automaticky, je pot�eba je p�idat ru�n�
               // Nap��klad ClaimType.Role se automaticky mapuje na Role claim, ale NameIdentifier ne
               options.Events.OnSigningIn = context =>
               {
                   // Z�sk�n� identity u�ivatele z kontextu
                   var identity = context.Principal?.Identity as ClaimsIdentity;

                   // Kontrola jestli u� existuje NameIdentifier claim
                   var userId = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                   // Pokud NameIdentifier neexistuje
                   if (userId == null)
                   {
                       // Hled�n� ID u�ivatele v jin�ch standardn�ch claimech (sub nebo nameid)
                       var userIdClaim = identity?.FindFirst("sub") ??
                                        identity?.FindFirst("nameid");

                       // Pokud byl nalezen claim s ID u�ivatele, p�id� se jako NameIdentifier
                       if (userIdClaim != null)
                       {
                           identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, userIdClaim.Value));
                       }
                   }

                   return Task.CompletedTask; //Proto�e se jedn� o ud�lost, je pot�eba vr�tit Task (p��padn� ud�lost deklarovat s async, pokud bych v n� m�l n�jak� await)
               };
           });
        // Add services to the container.
        builder.Services.AddRazorPages();

        builder.Services.AddControllers();

        // P�id�me slu�by pro session
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(expirationInHours);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        // P�id�me HttpClient pro vol�n� API
        builder.Services.AddHttpClient();

        // P�id�me t��du pro logov�n�
        builder.Services.AddSingleton<FileLogger>();

        // Vytvo��me instanci FileLoggeru p��mo
        var fileLogger = new FileLogger(builder.Configuration);

        // Pak p�id�me konfiguraci pro ASP.NET Core logging
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddProvider(new CustomFileLoggerProvider(fileLogger));
        });

        // P�id�me t��du pro p�eklady
        builder.Services.AddSingleton<Translator>();

        // Registrace HTML sanitizeru jako singleton
        builder.Services.AddSingleton<Ganss.Xss.IHtmlSanitizer>(provider => {
            var sanitizer = new Ganss.Xss.HtmlSanitizer();

            // Konfigurace sanitizeru

            // Povolen� HTML tagy
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
            sanitizer.AllowedTags.Add("figure"); // P�idan� pro zarovn�n� obr�zk�
            sanitizer.AllowedTags.Add("figcaption"); // P�idan� pro popisky obr�zk�

            // Povolen� HTML atributy
            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.Add("href");
            sanitizer.AllowedAttributes.Add("src");
            sanitizer.AllowedAttributes.Add("alt");
            sanitizer.AllowedAttributes.Add("class"); // D�le�it� pro zarovn�n�
            sanitizer.AllowedAttributes.Add("style"); // D�le�it� pro zarovn�n�
            sanitizer.AllowedAttributes.Add("align"); // Pro star�� zp�sob zarovn�n�
            sanitizer.AllowedAttributes.Add("width");
            sanitizer.AllowedAttributes.Add("height");

            sanitizer.KeepChildNodes = true;

            // Povolen� CSS styly
            sanitizer.AllowedCssProperties.Clear();
            sanitizer.AllowedCssProperties.Add("color");
            sanitizer.AllowedCssProperties.Add("font-weight");
            sanitizer.AllowedCssProperties.Add("text-align");
            sanitizer.AllowedCssProperties.Add("margin");
            sanitizer.AllowedCssProperties.Add("margin-left");
            sanitizer.AllowedCssProperties.Add("margin-right");
            sanitizer.AllowedCssProperties.Add("float"); // D�le�it� pro zarovn�n�
            sanitizer.AllowedCssProperties.Add("width");
            sanitizer.AllowedCssProperties.Add("height");
            sanitizer.AllowedCssProperties.Add("display");

            return sanitizer;
        });

        // Konfigurace pro p��stup z proxy
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Specifikujte p�esn�, kter� hlavi�ky chcete zpracov�vat
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // V produkci omezte d�v�ryhodn� proxy servery pouze na ty, kter� zn�me
            // Pokud nginx b�� na stejn�m serveru, m��ete d�v��ovat jen localhost
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            // P�idejte d�v�ryhodn� proxy servery
            options.KnownProxies.Add(System.Net.IPAddress.Parse("127.0.0.1"));
            options.KnownProxies.Add(System.Net.IPAddress.Parse("::1"));

            // Pokud m�te extern� proxy server, p�idejte jeho IP adresu
            // options.KnownProxies.Add(System.Net.IPAddress.Parse("IP_ADRESA_PROXY_SERVERU"));
        });

        var app = builder.Build();

        app.UseForwardedHeaders();

        // seznam rout, pro kter� se ihned vr�t� 404 a nepokra�uje se v pipeline
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

        // P�id�me middleware pro session
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseSession();
        app.MapControllers();
        app.MapRazorPages();


        var env = app.Services.GetRequiredService<IWebHostEnvironment>();
        string uploadsRoot = Path.Combine(env.WebRootPath, "uploads", "discussions");
        // Sma�e v�echny do�asn� adres��e (za��naj�c� "temp_") v adres��i uploads/discussions
        Zodpovedne.Web.Services.PictureCleaner.CleanTempDirectories(uploadsRoot);
        // Vol�n� metody pro smaz�n� neplatn�ch adres��� diskuz�
        Zodpovedne.Web.Services.PictureCleaner.CleanupInvalidDiscussionDirectories(uploadsRoot, builder.Configuration["ApiBaseUrl"]);

        app.Run();
    }
}
