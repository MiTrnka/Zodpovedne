// NuGet Microsoft.AspNetCore.Authentication.JwtBearer
// NuGet Swashbuckle.AspNetCore

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Zodpovedne.Data.Extensions;
using Zodpovedne.Data.Interfaces;
using Microsoft.OpenApi.Models;

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


            // Registrace slu�eb z projektu Data
            builder.Services.AddIdentityInfrastructure(builder.Configuration);

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

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();


            // Inicializace v�choz�ch rol� a admin ��tu p�i startu aplikace
            using (var scope = app.Services.CreateScope())
            {
                var identityDataSeeder = scope.ServiceProvider.GetRequiredService<IIdentityDataSeeder>();
                await identityDataSeeder.InitializeRolesAndAdminAsync();
            }

            // Po inicializaci rol� a admin ��tu
            using (var scope = app.Services.CreateScope())
            {
                var testDataSeeder = scope.ServiceProvider.GetRequiredService<ITestDataSeeder>();
                await testDataSeeder.SeedTestDataAsync();
            }

            app.Run();
        }
    }
}
