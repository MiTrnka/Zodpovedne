using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;
using Zodpovedne.GraphQL.Services;


namespace Zodpovedne.GraphQL;

/// <summary>
/// Třída Mutation seskupuje všechny operace, které mění data v aplikaci (vytváření, úpravy, mazání).
/// </summary>
public class Mutation
{
    // Deklarace soukromého pole pro uložení továrny na DbContext.
    // Klíčové slovo 'readonly' zajišťuje, že továrnu lze nastavit pouze jednou, a to v konstruktoru.
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly FirebaseNotificationService _notificationService;
    private readonly IConfiguration _configuration;

    public Mutation(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        FirebaseNotificationService notificationService,
        // Nechte si vstříknout IConfiguration
        IConfiguration configuration)
    {
        _contextFactory = contextFactory;
        _notificationService = notificationService;
        _configuration = configuration;
    }

    /// <summary>
    /// 'record' je moderní, neměnný typ pro přenos dat.
    /// Definuje strukturu vstupních dat, která klient musí poslat pro vytvoření nové zprávy.
    /// </summary>
    public record AddFreeMessageInput(string Nickname, string Text);

    /// <summary>
    /// Asynchronní metoda pro přidání nové zprávy do databáze.
    /// V GraphQL schématu bude dostupná jako pole 'addFreeMessage'.
    /// </summary>
    /// <param name="input">Vstupní objekt obsahující data pro novou zprávu.</param>
    /// <returns>Kompletní objekt právě vytvořené zprávy.</returns>
    public async Task<FreeMessage> AddFreeMessageAsync(AddFreeMessageInput input, string apiKey)
    {
        // Načteme klíč, který máme uložený na serveru v appsettings.Production.json
        var serverApiKey = _configuration["ApiKey"];

        // Zkontrolujeme, zda se klíč od klienta shoduje s naším
        if (string.IsNullOrEmpty(serverApiKey) || serverApiKey != apiKey)
        {
            // Pokud cokoliv selže, vrátíme obecnou chybu.
            throw new GraphQLException("Neplatný nebo chybějící API klíč.");
        }

        // Blok 'await using' vytvoří novou instanci DbContext a zaručí, že se po dokončení
        // operací bezpečně a automaticky "uklidí" (zavolá se metoda DisposeAsync).
        // To je naprosto klíčové pro správu prostředků a zabránění únikům paměti.
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Vytvoříme novou instanci C# entity 'FreeMessage' z dat, která poslal klient.
        var newMessage = new FreeMessage
        {
            // Převezmeme přezdívku ze vstupních dat.
            Nickname = input.Nickname,
            // Převezmeme text zprávy ze vstupních dat.
            Text = input.Text,
            // Nastavíme čas vytvoření na aktuální světový čas (UTC) přímo na serveru,
            // což je spolehlivější než spoléhat na čas nastavený u klienta.
            CreatedUtc = DateTime.UtcNow
        };

        // Přidáme nově vytvořený objekt 'newMessage' do kolekce FreeMessages.
        // Entity Framework Core nyní začne tuto entitu sledovat ve stavu "přidaná".
        context.FreeMessages.Add(newMessage);

        // Asynchronně uložíme všechny sledované změny do databáze.
        // V tomto okamžiku EF Core vygeneruje a spustí patřičný SQL příkaz (zde INSERT).
        await context.SaveChangesAsync();

        // Vrátíme klientovi kompletní objekt nové zprávy. To je dobrá praxe, protože
        // objekt může obsahovat serverem vygenerovaná data (jako je ID nebo čas vytvoření).
        return newMessage;
    }

    /// <summary>
    /// GraphQL mutace pro zaregistrování nebo aktualizaci FCM tokenu zařízení.
    /// Klient (MAUI aplikace) zavolá tuto mutaci, aby serveru sdělil svou "adresu" pro push notifikace.
    /// </summary>
    /// <param name="token">Samotný FCM registrační token získaný ze zařízení.</param>
    /// <param name="context">Instance DbContextu, kterou nám automaticky poskytne Hot Chocolate.</param>
    /// <returns>Jednoduchý boolean, který potvrdí, zda operace proběhla úspěšně.</returns>
    public async Task<bool> RegisterFcmTokenAsync(string token)
    {
        // Vstupní validace - zajistíme, že nám klient neposlal prázdný token.
        if (string.IsNullOrWhiteSpace(token))
        {
            // V reálné aplikaci bychom zde mohli vyhodit specifickou GraphQL výjimku,
            // ale pro náš účel stačí vrátit 'false'.
            return false;
        }

        // Vytvoříme si novou instanci DbContextu z naší továrny.
        // 'await using' zajistí, že se po dokončení operace správně uvolní všechny prostředky.
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Zkusíme najít, zda už tento token v databázi náhodou nemáme.
        // Tím zabráníme duplicitám a zbytečným zápisům.
        var existingToken = await context.FcmRegistrationTokens
                                         .FirstOrDefaultAsync(t => t.Token == token);

        // Pokud token v databázi ještě neexistuje...
        if (existingToken == null)
        {
            // ...vytvoříme novou entitu.
            var newToken = new FcmRegistrationToken
            {
                Token = token,
                CreatedUtc = DateTime.UtcNow // Uložíme si čas vytvoření.
            };

            // Přidáme novou entitu do DbContextu. Entity Framework ji nyní sleduje jako "přidanou".
            context.FcmRegistrationTokens.Add(newToken);

            // Uložíme změny do databáze. EF Core vygeneruje a spustí SQL příkaz INSERT.
            await context.SaveChangesAsync();
        }
        // Pokud token již existuje, nemusíme dělat nic. Mohli bychom zde například
        // aktualizovat časové razítko, ale pro náš jednoduchý případ to není nutné.

        // Vrátíme 'true' na znamení, že operace proběhla úspěšně.
        return true;
    }

    /// <summary>
    /// GraphQL mutace, která spustí odeslání globální notifikace.
    /// VYŽADUJE API KLÍČ PRO AUTORIZACI.
    /// </summary>
    public async Task<string> SendGlobalNotificationAsync(string title, string body, string apiKey)
    {
        // Načteme klíč, který máme uložený na serveru
        var serverApiKey = _configuration["ApiKey"];

        // Zkontrolujeme, zda se klíč od klienta shoduje s naším
        if (string.IsNullOrEmpty(serverApiKey) || serverApiKey != apiKey)
        {
            // Pokud cokoliv selže, vrátíme obecnou chybu.
            throw new GraphQLException("Neplatný nebo chybějící API klíč.");
        }

        // Pokud je klíč v pořádku, pokračujeme v původní logice
        return await _notificationService.SendGlobalNotificationAsync(title, body);
    }
}
