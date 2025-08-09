// Zodpovedne.GraphQL/Mutation.cs
// Tato třída je protějškem Query a definuje všechny operace, které mění data (vytvoření, úprava, smazání).

using HotChocolate.Data;
using HotChocolate.Types;
using Zodpovedne.Data;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;

namespace Zodpovedne.GraphQL
{
    public class Mutation
    {
        // 'record' je moderní, zjednodušená forma třídy, ideální pro přenos dat.
        // Použití specializovaného vstupního typu (Input Type) je best practice:
        // 1. Zvyšuje bezpečnost - klient nemůže poslat víc dat, než povolíme.
        // 2. Zlepšuje čitelnost schématu - je jasné, co metoda očekává.
        public record AddFreeMessageInput(string Nickname, string Text);

        // Atribut [UseDbContext] zde funguje stejně jako v Query - stará se o DbContext.
        [UseDbContext(typeof(ApplicationDbContext))]

        // Asynchronní metoda pro přidání nové zprávy. Bude v GraphQL schématu
        // dostupná jako pole 'addFreeMessage'.
        public async Task<FreeMessage> AddFreeMessageAsync(
            // Vstupní argumenty metody, které klient posílá v dotazu.
            AddFreeMessageInput input,
            [Service] ApplicationDbContext context)
        {
            // Vytvoříme novou instanci naší C# entity.
            var newMessage = new FreeMessage
            {
                Nickname = input.Nickname,
                Text = input.Text,
                CreatedUtc = DateTime.UtcNow // Čas je nejlepší nastavovat na serveru.
            };

            // Řekneme Entity Frameworku, aby začal sledovat tuto novou entitu
            // a připravil si ji pro vložení do databáze.
            context.FreeMessages.Add(newMessage);

            // Asynchronně uložíme všechny sledované změny (v našem případě jednu novou
            // zprávu) do databáze. Zde se provede reálný SQL INSERT.
            await context.SaveChangesAsync();

            // Podle GraphQL konvence je dobré vrátit data, která byla právě změněna.
            return newMessage;
        }
    }
}