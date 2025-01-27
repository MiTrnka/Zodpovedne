// NuGet Microsoft.AspNetCore.Authentication.JwtBearer
// NuGet Swashbuckle.AspNetCore

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Zodpovedne.Data.Extensions;
using Zodpovedne.Data.Interfaces;

namespace Zodpovedne.RESTAPI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Registrace služeb z projektu Data
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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
                    NameClaimType = ClaimTypes.Email,
                    RoleClaimType = ClaimTypes.Role
                };
            });

            builder.Services.AddAuthorization(options => {
                // Definice politik - v produkci rozšíøit podle potøeb
                options.AddPolicy("RequireAdminRole", policy =>
                    policy.RequireRole("Admin"));
                options.AddPolicy("RequireUserRole", policy =>
                    policy.RequireRole("User"));
            });

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            // Inicializace výchozích rolí a admin úètu pøi startu aplikace
            using (var scope = app.Services.CreateScope())
            {
                var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
                await identityService.InitializeRolesAndAdminAsync();
            }

            app.Run();
        }
    }
}
