// NuGet Microsoft.AspNetCore.Authentication.JwtBearer
// NuGet Swashbuckle.AspNetCore
// NuGet HtmlSanitizer   //pro bezpeèné èištìní HTML vstupu

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

namespace Zodpovedne.RESTAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Podmíneèná konfigurace podle prostøedí
            if (builder.Environment.IsDevelopment())
            {
                // Vývojové prostøedí
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(5555); // Lokální vývojový port pro API
                });
            }
            else
            {
                // Produkèní prostøedí - nastavení pro Nginx
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(5001); // Port, na kterém bude API poslouchat
                });
                // nastavuje, kam se budou ukládat šifrovací klíèe pro ASP.NET Core DataProtection
                // možná komplikace, pokud API bude bìžet na více serverech, protože klíèe nebudou sdíleny
                // v takovém pøípadì je do budoucna lepší použít nìjaký externí úložištì, napø. Azure Key Vault
                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo("/var/www/zodpovedne/keys"));
            }

            // Kontrola existence konfiguraèního souboru a jeho položek
            if (builder.Configuration == null)
            {
                Console.WriteLine("Program skonèil, protože nebyl nalezen konfiguraèní soubor.");
                return;
            }
            IConfiguration configuration = builder.Configuration;

            if (configuration["Jwt:Issuer"] == null)
            {
                Console.WriteLine("JWT Issuer není vyplnìn v konfiguraèním souboru");
                return;
            }
            if (configuration["Jwt:Audience"] == null)
            {
                Console.WriteLine("JWT Audience není vyplnìn v konfiguraèním souboru");
                return;
            }
            if (configuration["Jwt:Key"] == null)
            {
                Console.WriteLine("JWT Key není vyplnìn v konfiguraèním souboru");
                return;
            }
            if (configuration["Jwt:ExpirationInHours"] == null)
            {
                Console.WriteLine("JWT ExpirationInHours není vyplnìn v konfiguraèním souboru");
                return;
            }

            // Registrace služeb pro response caching, nastavení se týká celé aplikace pro všechny endpointy oznaèené atributem [ResponseCache]
            builder.Services.AddResponseCaching(options =>
            {
                // Maximální velikost jedné položky v cache je 10 MB. Pokud by nìjaká HTTP odpovìï mìla vìtší velikost, nebude cachována.
                options.MaximumBodySize = 10 * 1024 * 1024;
                // celková maximální velikost všech položek v cache je 1000 MB. Když tento limit bude pøekroèen, nejstarší nebo nejménì používané položky budou odstranìny z cache.
                options.SizeLimit = 1000 * 1024 * 1024;
            });

            // Registrace služeb z projektu Data
            //builder.Services.AddIdentityInfrastructure(builder.Configuration);


            // Naètení connection stringu z konfigurace
            string? connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Connection string 'DefaultConnection' není nastaven v konfiguraci Data projektu.");

            // Registrace DbContextu
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Registrace IDataContext
            // Když nìkdo požádá o IDataContext, dostane instanci ApplicationDbContext
            /*builder.Services.AddScoped<IDataContext>(sp =>
                sp.GetRequiredService<ApplicationDbContext>());*/

            // Registrace služeb pro Identity
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
                // Pùvodní konfigurace z ServiceCollectionExtensions
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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? throw new ArgumentNullException("JWT Key není vyplnìn v konfiguraèním souboru"))),
                    NameClaimType = ClaimTypes.Email,
                    RoleClaimType = ClaimTypes.Role
                };
            });

            builder.Services.AddAuthorization(options => {
                // Definice politik - v produkci rozšíøit podle potøeb
                options.AddPolicy("RequireAdminRole", policy =>
                    policy.RequireRole("Admin"));
                options.AddPolicy("RequireMemberRole", policy =>
                    policy.RequireRole("Member"));
            });


            // Pøidání CORS do kontejneru služeb
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins",
                    policy =>
                    {
                        if (builder.Environment.IsDevelopment())
                        {
                            policy
                                .SetIsOriginAllowed(origin => true) // Povolí jakýkoliv origin v development módu
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
                                 "https://api.mamouzodpovedne.sk"
                             )
                             .AllowAnyMethod()
                             .AllowAnyHeader()
                             .AllowCredentials();
                        }
                    });
            });

            // Registrace služby pro odesílání e-mailù
            builder.Services.AddScoped<IEmailService, EmailService>();

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

            builder.Services.AddControllers();

            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddEndpointsApiExplorer();

                // Registrace Swaggeru, v produkci zakomentovat, v závorce je nepovinná èást, která konfiguruje pouze to, abych mohl vložit token v Swaggeru do hlavièky Authorization
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Zodpovedne API", Version = "v1" });

                    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Description = "JWT Authorization header. Just enter the token",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.Http,  // Zmìna zde
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

            // Pøidání pamìové cache napøíklad pro cachování dat z databáze (seznam diskuzí, kde nìkdo reagoval na mùj komentáø)
            builder.Services.AddMemoryCache();

            var app = builder.Build();

            // seznam rout, pro které se ihned vrátí 404 a nepokraèuje se v pipeline
            app.MapShortCircuit(404, "wp-admin", "wp-login");

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                //app.UseHttpsRedirection();
            }

            // Pøidání middleware pro response caching
            app.UseResponseCaching();
            // Middleware pro nastavení cache headers
            app.Use(async (context, next) =>
            {
                // Výchozí cache control hodnoty
                context.Response.GetTypedHeaders().CacheControl =
                    new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        // Pøidá do http hlavièky Cache-Control: public, max-age=2
                        // I klientské prohlížeèe, veøejné proxyservery a cdn mohou cachovat odpovìï
                        Public = true,

                        // defaultní doba života v takovéto keší (prohlíže, cdn...) cache je 1 sekunda, dobu pak nastavím u konkrétních endpointù
                        // napø: [ResponseCache(Duration = 360)], které nastaví èas uchování jak na klientovi, tak na serveru
                        MaxAge = TimeSpan.FromSeconds(1)
                    };

                await next();
            });

            app.UseCors("AllowSpecificOrigins");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();


            // Inicializace výchozích rolí a admin úètu pøi startu aplikace
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
