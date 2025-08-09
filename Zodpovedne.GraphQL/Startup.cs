// Zodpovedne.GraphQL/Startup.cs

using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data;
using Zodpovedne.Data.Data;

namespace Zodpovedne.GraphQL
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var connectionString = Configuration.GetConnectionString("DefaultConnection");

            // --- ZMĚNA ZDE ---
            // Místo AddDbContext použijeme AddDbContextFactory, kterou vyžaduje HotChocolate
            // pro správnou a bezpečnou práci s databází ve více vláknech.
            services.AddDbContextFactory<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            services
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .AddMutationType<Mutation>()
                .AddProjections()
                .AddFiltering()
                .AddSorting();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGraphQL();
            });
        }
    }
}