using Microsoft.AspNetCore.Authentication.Cookies;

namespace Zodpovedne.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Cookie autentizace pro razor pages
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.ExpireTimeSpan = TimeSpan.FromHours(12); // Stejná doba jako JWT
            });

        // Add services to the container.
        builder.Services.AddRazorPages();

        // Pøidáme služby pro session
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(12);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        // Pøidáme HttpClient pro volání API
        builder.Services.AddHttpClient();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
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
