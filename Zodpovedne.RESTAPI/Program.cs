// NuGet Microsoft.AspNetCore.Authentication.JwtBearer
// NuGet Swashbuckle.AspNetCore
// NuGet HtmlSanitizer   //pro bezpe�n� �i�t�n� HTML vstupu

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Zodpovedne.Data.Extensions;
using Microsoft.OpenApi.Models;
using Zodpovedne.Logging;

namespace Zodpovedne.RESTAPI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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
                // celkov� maxim�ln� velikost v�ech polo�ek v cache je 100 MB. Kdy� tento limit bude p�ekro�en, nejstar�� nebo nejm�n� pou��van� polo�ky budou odstran�ny z cache.
                options.SizeLimit = 100 * 1024 * 1024;
            });

            // Registrace slu�eb z projektu Data
            //builder.Services.AddIdentityInfrastructure(builder.Configuration);

            // Konfigurace/registrace datov� vrstvy (DBContext, Identity)
            builder.Services.AddDataLayer();

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
                        policy.WithOrigins(
                                "http://localhost:5214",
                                "http://192.168.0.213:5214",
                                "http://192.168.0.214:5214"
                            )
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials();
                    });
            });

            builder.Services.AddControllers();
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

            // P�id�me t��du pro logov�n�
            builder.Services.AddSingleton<FileLogger>();

            // Pak p�id�me konfiguraci pro ASP.NET Core logging za pou�it� na�eho FileLoggeru
            builder.Services.AddLogging(logging =>
            {
                logging.ClearProviders(); // Odstran� v�choz� loggery
                logging.AddConsole(); // Ponech� logov�n� do konzole

                // P�id� n� vlastn� logger pro kritick� chyby
                logging.AddProvider(new CustomFileLoggerProvider(
                    logging.Services.BuildServiceProvider().GetRequiredService<FileLogger>()
                ));
            });

            // P�id�n� pam�ov� cache nap��klad pro cachov�n� dat z datab�ze (seznam diskuz�, kde n�kdo reagoval na m�j koment��)
            builder.Services.AddMemoryCache();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseHttpsRedirection();
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
            /*using (var scope = app.Services.CreateScope())
            {
                var identityDataSeeder = scope.ServiceProvider.GetRequiredService<IIdentityDataSeeder>();
                await identityDataSeeder.InitializeRolesAndAdminAsync();
            }*/

            //Po inicializaci rol� a admin ��tu
            /*using (var scope = app.Services.CreateScope())
            {
                var testDataSeeder = scope.ServiceProvider.GetRequiredService<ITestDataSeeder>();
                await testDataSeeder.SeedTestDataAsync();
            }*/

            app.Run();
        }
    }
}
