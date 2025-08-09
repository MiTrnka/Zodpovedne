namespace Zodpovedne.GraphQL;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ---------------------------------
        // KONFIGURACE SLUŽEB (SERVICES)
        // ---------------------------------

        // Zaregistrujeme služby pro GraphQL server a øekneme mu,
        // aby jako zdroj pro dotazy použil naši tøídu Query.
        builder.Services
            .AddGraphQLServer()
            .AddQueryType<Query>();

        // Pøidáme služby potøebné pro generování Swagger/OpenAPI dokumentace.
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // ---------------------------------
        // SESTAVENÍ APLIKACE A KONFIGURACE PIPELINE
        // ---------------------------------

        var app = builder.Build();

        // Namapujeme GraphQL endpoint na standardní adresu "/graphql".
        // Tento pøíkaz také zpøístupní testovací prostøedí Banana Cake Pop.
        app.MapGraphQL();

        // Konfigurace HTTP pipeline.
        if (app.Environment.IsDevelopment())
        {
            // Pokud je aplikace ve vývojovém prostøedí, zapneme Swagger UI.
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Zapneme automatické pøesmìrování z HTTP na HTTPS.
        app.UseHttpsRedirection();

        // Spustíme aplikaci.
        app.Run();
    }
}