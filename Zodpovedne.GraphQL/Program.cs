namespace Zodpovedne.GraphQL;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ---------------------------------
        // KONFIGURACE SLU�EB (SERVICES)
        // ---------------------------------

        // Zaregistrujeme slu�by pro GraphQL server a �ekneme mu,
        // aby jako zdroj pro dotazy pou�il na�i t��du Query.
        builder.Services
            .AddGraphQLServer()
            .AddQueryType<Query>();

        // P�id�me slu�by pot�ebn� pro generov�n� Swagger/OpenAPI dokumentace.
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // ---------------------------------
        // SESTAVEN� APLIKACE A KONFIGURACE PIPELINE
        // ---------------------------------

        var app = builder.Build();

        // Namapujeme GraphQL endpoint na standardn� adresu "/graphql".
        // Tento p��kaz tak� zp��stupn� testovac� prost�ed� Banana Cake Pop.
        app.MapGraphQL();

        // Konfigurace HTTP pipeline.
        if (app.Environment.IsDevelopment())
        {
            // Pokud je aplikace ve v�vojov�m prost�ed�, zapneme Swagger UI.
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Zapneme automatick� p�esm�rov�n� z HTTP na HTTPS.
        app.UseHttpsRedirection();

        // Spust�me aplikaci.
        app.Run();
    }
}