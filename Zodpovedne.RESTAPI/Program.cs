// NuGet Microsoft.AspNetCore.Authentication.JwtBearer
// NuGet Swashbuckle.AspNetCore
// NuGet HtmlSanitizer   //pro bezpeËnÈ ËiötÏnÌ HTML vstupu

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Microsoft.OpenApi.Models;
using Zodpovedne.Logging;
using Zodpovedne.RESTAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;
using Zodpovedne.Logging.Services;
using Microsoft.AspNetCore.DataProtection;
using Zodpovedne.Data.Services;
using Microsoft.AspNetCore.HttpOverrides;

namespace Zodpovedne.RESTAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // PodmÌneËn· konfigurace podle prost¯edÌ
            if (builder.Environment.IsDevelopment())
            {
                // V˝vojovÈ prost¯edÌ
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(5555); // Lok·lnÌ v˝vojov˝ port pro API
                });
            }
            else
            {
                // ProdukËnÌ prost¯edÌ - nastavenÌ pro Nginx
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(5001); // Port, na kterÈm bude API poslouchat
                                               // NastavenÌ pro pr·ci za proxy
                    options.ConfigureHttpsDefaults(httpsOptions =>
                    {
                        httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
                    });
                });
                // nastavuje, kam se budou ukl·dat öifrovacÌ klÌËe pro ASP.NET Core DataProtection
                // moûn· komplikace, pokud API bude bÏûet na vÌce serverech, protoûe klÌËe nebudou sdÌleny
                // v takovÈm p¯ÌpadÏ je do budoucna lepöÌ pouûÌt nÏjak˝ externÌ ˙loûiötÏ, nap¯. Azure Key Vault
                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo("/var/www/discussion/keys"));
            }

            // Kontrola existence konfiguraËnÌho souboru a jeho poloûek
            if (builder.Configuration == null)
            {
                Console.WriteLine("Program skonËil, protoûe nebyl nalezen konfiguraËnÌ soubor.");
                return;
            }
            IConfiguration configuration = builder.Configuration;

            if (configuration["Jwt:Issuer"] == null)
            {
                Console.WriteLine("JWT Issuer nenÌ vyplnÏn v konfiguraËnÌm souboru");
                return;
            }
            if (configuration["Jwt:Audience"] == null)
            {
                Console.WriteLine("JWT Audience nenÌ vyplnÏn v konfiguraËnÌm souboru");
                return;
            }
            if (configuration["Jwt:Key"] == null)
            {
                Console.WriteLine("JWT Key nenÌ vyplnÏn v konfiguraËnÌm souboru");
                return;
            }
            if (configuration["Jwt:ExpirationInHours"] == null)
            {
                Console.WriteLine("JWT ExpirationInHours nenÌ vyplnÏn v konfiguraËnÌm souboru");
                return;
            }

            // Registrace sluûeb pro response caching, nastavenÌ se t˝k· celÈ aplikace pro vöechny endpointy oznaËenÈ atributem [ResponseCache]
            builder.Services.AddResponseCaching(options =>
            {
                // Maxim·lnÌ velikost jednÈ poloûky v cache je 10 MB. Pokud by nÏjak· HTTP odpovÏÔ mÏla vÏtöÌ velikost, nebude cachov·na.
                options.MaximumBodySize = 10 * 1024 * 1024;
                // celkov· maxim·lnÌ velikost vöech poloûek v cache je 1000 MB. Kdyû tento limit bude p¯ekroËen, nejstaröÌ nebo nejmÈnÏ pouûÌvanÈ poloûky budou odstranÏny z cache.
                options.SizeLimit = 1000 * 1024 * 1024;
            });

            // Registrace sluûeb z projektu Data
            //builder.Services.AddIdentityInfrastructure(builder.Configuration);


            // NaËtenÌ connection stringu z konfigurace
            string? connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Connection string 'DefaultConnection' nenÌ nastaven v konfiguraci Data projektu.");

            // Registrace DbContextu
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Registrace IDataContext
            // Kdyû nÏkdo poû·d· o IDataContext, dostane instanci ApplicationDbContext
            /*builder.Services.AddScoped<IDataContext>(sp =>
                sp.GetRequiredService<ApplicationDbContext>());*/

            // Registrace sluûeb pro Identity
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
                // P˘vodnÌ konfigurace z ServiceCollectionExtensions
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 1;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // Konfigurace JWT autentizace
            builder.Services.AddAuthentication(options => {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options => {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? throw new ArgumentNullException("JWT Key nenÌ vyplnÏn v konfiguraËnÌm souboru"))),
                    NameClaimType = ClaimTypes.Email,
                    RoleClaimType = ClaimTypes.Role
                };
            });

            builder.Services.AddAuthorization(options => {
                // Definice politik - v produkci rozöÌ¯it podle pot¯eb
                options.AddPolicy("RequireAdminRole", policy =>
                    policy.RequireRole("Admin"));
                options.AddPolicy("RequireMemberRole", policy =>
                    policy.RequireRole("Member"));
            });


            // P¯id·nÌ CORS do kontejneru sluûeb
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins",
                    policy =>
                    {
                        if (builder.Environment.IsDevelopment())
                        {
                            policy
                                .SetIsOriginAllowed(origin => true) // PovolÌ jak˝koliv origin v development mÛdu
                                .AllowAnyMethod()
                                .AllowAnyHeader()
                                .AllowCredentials();
                        }
                        else
                        {
                            policy.WithOrigins(
                                 "https://mamouzodpovedne.cz",
                                 "https://www.mamouzodpovedne.cz",
                                 "https://mamouzodpovedne.sk",
                                 "https://www.mamouzodpovedne.sk",
                                 "https://mamazodpovedne.cz",
                                 "https://www.mamazodpovedne.cz",
                                 "https://mamazodpovedne.sk",
                                 "https://www.mamazodpovedne.sk",
                                 "https://api.mamazodpovedne.cz",
                                 "https://api.mamouzodpovedne.cz",
                                 "https://api.mamazodpovedne.sk",
                                 "https://api.mamouzodpovedne.sk",

                                 "https://freediscussion.cz",
                                 "https://www.freediscussion.cz",
                                 "https://api.freediscussion.cz",
                                 "https://freediscussions.cz",
                                 "https://www.freediscussions.cz",
                                 "https://api.freediscussions.cz",

                                 "https://mydiscussion.cz",
                                 "https://www.mydiscussion.cz",
                                 "https://api.mydiscussion.cz",
                                 "https://mydiscussions.cz",
                                 "https://www.mydiscussions.cz",
                                 "https://api.mydiscussions.cz",

                                 "https://discussion.cz",
                                 "https://www.discussion.cz",
                                 "https://api.discussion.cz",
                                 "https://discussions.cz",
                                 "https://www.discussions.cz",
                                 "https://api.discussions.cz"

                             )
                             .AllowAnyMethod()
                             .AllowAnyHeader()
                             .AllowCredentials();
                        }
                    });
            });

            // Registrace sluûby pro odesÌl·nÌ e-mail˘
            builder.Services.AddScoped<IEmailService, EmailService>();

            // P¯id·me HttpClient pro vol·nÌ API
            builder.Services.AddHttpClient();

            // P¯id·me t¯Ìdu pro logov·nÌ
            builder.Services.AddSingleton<FileLogger>();

            // Vytvo¯Ìme instanci FileLoggeru p¯Ìmo
            var fileLogger = new FileLogger(builder.Configuration);

            // Pak p¯id·me konfiguraci pro ASP.NET Core logging
            builder.Services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddProvider(new CustomFileLoggerProvider(fileLogger));
            });

            // P¯id·me t¯Ìdu pro p¯eklady
            builder.Services.AddSingleton<Translator>();

            // Registrace HTML sanitizeru jako singleton
            builder.Services.AddSingleton<Ganss.Xss.IHtmlSanitizer>(provider => {
                var sanitizer = new Ganss.Xss.HtmlSanitizer();

                // Konfigurace sanitizeru

                // PovolenÈ HTML tagy
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
                sanitizer.AllowedTags.Add("figure"); // P¯idan˝ pro zarovn·nÌ obr·zk˘
                sanitizer.AllowedTags.Add("figcaption"); // P¯idan˝ pro popisky obr·zk˘

                // PovolenÈ HTML atributy
                sanitizer.AllowedAttributes.Clear();
                sanitizer.AllowedAttributes.Add("href");
                sanitizer.AllowedAttributes.Add("src");
                sanitizer.AllowedAttributes.Add("alt");
                sanitizer.AllowedAttributes.Add("class"); // D˘leûitÈ pro zarovn·nÌ
                sanitizer.AllowedAttributes.Add("style"); // D˘leûitÈ pro zarovn·nÌ
                sanitizer.AllowedAttributes.Add("align"); // Pro staröÌ zp˘sob zarovn·nÌ
                sanitizer.AllowedAttributes.Add("width");
                sanitizer.AllowedAttributes.Add("height");

                sanitizer.KeepChildNodes = true;

                // PovolenÈ CSS styly
                sanitizer.AllowedCssProperties.Clear();
                sanitizer.AllowedCssProperties.Add("color");
                sanitizer.AllowedCssProperties.Add("font-weight");
                sanitizer.AllowedCssProperties.Add("text-align");
                sanitizer.AllowedCssProperties.Add("margin");
                sanitizer.AllowedCssProperties.Add("margin-left");
                sanitizer.AllowedCssProperties.Add("margin-right");
                sanitizer.AllowedCssProperties.Add("float"); // D˘leûitÈ pro zarovn·nÌ
                sanitizer.AllowedCssProperties.Add("width");
                sanitizer.AllowedCssProperties.Add("height");
                sanitizer.AllowedCssProperties.Add("display");

                return sanitizer;
            });

            builder.Services.AddControllers();

            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddEndpointsApiExplorer();

                // Registrace Swaggeru, v produkci zakomentovat, v z·vorce je nepovinn· Ë·st, kter· konfiguruje pouze to, abych mohl vloûit token v Swaggeru do hlaviËky Authorization
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Zodpovedne API", Version = "v1" });

                    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Description = "JWT Authorization header. Just enter the token",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.Http,  // ZmÏna zde
                        Scheme = "bearer",  // a zde
                        BearerFormat = "JWT"
                    });

                    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                    });
                });
            }

            // P¯id·nÌ pamÏùovÈ cache nap¯Ìklad pro cachov·nÌ dat z datab·ze (seznam diskuzÌ, kde nÏkdo reagoval na m˘j koment·¯)
            builder.Services.AddMemoryCache();

            // Konfigurace pro pr·ci za proxy
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                // P¯ÌmÈ nastavenÌ hlaviËek, kterÈ chceme zpracov·vat
                options.ForwardedHeaders = ForwardedHeaders.All;

                // PovolenÌ vöech proxy server˘ a sÌtÌ
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();

                // D˘leûitÈ - povolit uûivatelskÈ hlaviËky
                options.ForwardedForHeaderName = "X-Forwarded-For";
                options.ForwardedHostHeaderName = "X-Forwarded-Host";
                options.ForwardedProtoHeaderName = "X-Forwarded-Proto";
                options.OriginalHostHeaderName = "Host";
                options.OriginalForHeaderName = "X-Original-For";

                // Povolit hlaviËku X-Real-IP
                options.ForwardedForHeaderName = "X-Real-IP";
            });

            var app = builder.Build();

            app.UseForwardedHeaders();

            // seznam rout, pro kterÈ se ihned vr·tÌ 404 a nepokraËuje se v pipeline
            app.MapShortCircuit(404, "wp-admin", "wp-login", "sitemap.xml", "robots.txt");

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                //app.UseHttpsRedirection();
            }

            // P¯id·nÌ middleware pro response caching
            app.UseResponseCaching();
            // Middleware pro nastavenÌ cache headers
            app.Use(async (context, next) =>
            {
                // V˝chozÌ cache control hodnoty
                context.Response.GetTypedHeaders().CacheControl =
                    new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        // P¯id· do http hlaviËky Cache-Control: public, max-age=2
                        // I klientskÈ prohlÌûeËe, ve¯ejnÈ proxyservery a cdn mohou cachovat odpovÏÔ
                        Public = true,

                        // defaultnÌ doba ûivota v takovÈto keöÌ (prohlÌûe, cdn...) cache je 1 sekunda, dobu pak nastavÌm u konkrÈtnÌch endpoint˘
                        // nap¯: [ResponseCache(Duration = 360)], kterÈ nastavÌ Ëas uchov·nÌ jak na klientovi, tak na serveru
                        MaxAge = TimeSpan.FromSeconds(1)
                    };

                await next();
            });

            app.UseCors("AllowSpecificOrigins");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();


            // Inicializace v˝chozÌch rolÌ a admin ˙Ëtu p¯i startu aplikace
            /*var scope = app.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var initializer = new DataInitializer(dbContext, userManager, roleManager);
            initializer.InitializeAsync(true).Wait();*/

            app.Run();
        }
    }
}
