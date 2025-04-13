// NuGet Microsoft.AspNetCore.Authentication.JwtBearer
// NuGet Swashbuckle.AspNetCore
// NuGet HtmlSanitizer   //pro bezpe�n� �i�t�n� HTML vstupu

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

            // Podm�ne�n� konfigurace podle prost�ed�
            if (builder.Environment.IsDevelopment())
            {
                // V�vojov� prost�ed�
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(5555); // Lok�ln� v�vojov� port pro API
                });
            }
            else
            {
                // Produk�n� prost�ed� - nastaven� pro Nginx
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(5001); // Port, na kter�m bude API poslouchat
                });
                // nastavuje, kam se budou ukl�dat �ifrovac� kl��e pro ASP.NET Core DataProtection
                // mo�n� komplikace, pokud API bude b�et na v�ce serverech, proto�e kl��e nebudou sd�leny
                // v takov�m p��pad� je do budoucna lep�� pou��t n�jak� extern� �lo�i�t�, nap�. Azure Key Vault
                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo("/var/www/zodpovedne/keys"));
            }

            // Kontrola existence konfigura�n�ho souboru a jeho polo�ek
            if (builder.Configuration == null)
            {
                Console.WriteLine("Program skon�il, proto�e nebyl nalezen konfigura�n� soubor.");
                return;
            }
            IConfiguration configuration = builder.Configuration;

            if (configuration["Jwt:Issuer"] == null)
            {
                Console.WriteLine("JWT Issuer nen� vypln�n v konfigura�n�m souboru");
                return;
            }
            if (configuration["Jwt:Audience"] == null)
            {
                Console.WriteLine("JWT Audience nen� vypln�n v konfigura�n�m souboru");
                return;
            }
            if (configuration["Jwt:Key"] == null)
            {
                Console.WriteLine("JWT Key nen� vypln�n v konfigura�n�m souboru");
                return;
            }
            if (configuration["Jwt:ExpirationInHours"] == null)
            {
                Console.WriteLine("JWT ExpirationInHours nen� vypln�n v konfigura�n�m souboru");
                return;
            }

            // Registrace slu�eb pro response caching, nastaven� se t�k� cel� aplikace pro v�echny endpointy ozna�en� atributem [ResponseCache]
            builder.Services.AddResponseCaching(options =>
            {
                // Maxim�ln� velikost jedn� polo�ky v cache je 10 MB. Pokud by n�jak� HTTP odpov�� m�la v�t�� velikost, nebude cachov�na.
                options.MaximumBodySize = 10 * 1024 * 1024;
                // celkov� maxim�ln� velikost v�ech polo�ek v cache je 1000 MB. Kdy� tento limit bude p�ekro�en, nejstar�� nebo nejm�n� pou��van� polo�ky budou odstran�ny z cache.
                options.SizeLimit = 1000 * 1024 * 1024;
            });

            // Registrace slu�eb z projektu Data
            //builder.Services.AddIdentityInfrastructure(builder.Configuration);


            // Na�ten� connection stringu z konfigurace
            string? connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Connection string 'DefaultConnection' nen� nastaven v konfiguraci Data projektu.");

            // Registrace DbContextu
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Registrace IDataContext
            // Kdy� n�kdo po��d� o IDataContext, dostane instanci ApplicationDbContext
            /*builder.Services.AddScoped<IDataContext>(sp =>
                sp.GetRequiredService<ApplicationDbContext>());*/

            // Registrace slu�eb pro Identity
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
                // P�vodn� konfigurace z ServiceCollectionExtensions
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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? throw new ArgumentNullException("JWT Key nen� vypln�n v konfigura�n�m souboru"))),
                    NameClaimType = ClaimTypes.Email,
                    RoleClaimType = ClaimTypes.Role
                };
            });

            builder.Services.AddAuthorization(options => {
                // Definice politik - v produkci roz���it podle pot�eb
                options.AddPolicy("RequireAdminRole", policy =>
                    policy.RequireRole("Admin"));
                options.AddPolicy("RequireMemberRole", policy =>
                    policy.RequireRole("Member"));
            });


            // P�id�n� CORS do kontejneru slu�eb
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins",
                    policy =>
                    {
                        if (builder.Environment.IsDevelopment())
                        {
                            policy
                                .SetIsOriginAllowed(origin => true) // Povol� jak�koliv origin v development m�du
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

            // Registrace slu�by pro odes�l�n� e-mail�
            builder.Services.AddScoped<IEmailService, EmailService>();

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

            builder.Services.AddControllers();

            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddEndpointsApiExplorer();

                // Registrace Swaggeru, v produkci zakomentovat, v z�vorce je nepovinn� ��st, kter� konfiguruje pouze to, abych mohl vlo�it token v Swaggeru do hlavi�ky Authorization
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Zodpovedne API", Version = "v1" });

                    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Description = "JWT Authorization header. Just enter the token",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.Http,  // Zm�na zde
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

            // P�id�n� pam�ov� cache nap��klad pro cachov�n� dat z datab�ze (seznam diskuz�, kde n�kdo reagoval na m�j koment��)
            builder.Services.AddMemoryCache();

            var app = builder.Build();

            // seznam rout, pro kter� se ihned vr�t� 404 a nepokra�uje se v pipeline
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

            // P�id�n� middleware pro response caching
            app.UseResponseCaching();
            // Middleware pro nastaven� cache headers
            app.Use(async (context, next) =>
            {
                // V�choz� cache control hodnoty
                context.Response.GetTypedHeaders().CacheControl =
                    new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        // P�id� do http hlavi�ky Cache-Control: public, max-age=2
                        // I klientsk� prohl�e�e, ve�ejn� proxyservery a cdn mohou cachovat odpov��
                        Public = true,

                        // defaultn� doba �ivota v takov�to ke�� (prohl�e, cdn...) cache je 1 sekunda, dobu pak nastav�m u konkr�tn�ch endpoint�
                        // nap�: [ResponseCache(Duration = 360)], kter� nastav� �as uchov�n� jak na klientovi, tak na serveru
                        MaxAge = TimeSpan.FromSeconds(1)
                    };

                await next();
            });

            app.UseCors("AllowSpecificOrigins");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();


            // Inicializace v�choz�ch rol� a admin ��tu p�i startu aplikace
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
