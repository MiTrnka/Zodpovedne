// Zodpovedne.GraphQL/Query.cs
// Tato třída definuje "vstupní brány" pro všechny dotazy na čtení dat. Každá veřejná metoda zde
// představuje jedno pole (field), na které se lze v GraphQL dotazu ptát.

using HotChocolate.Data;
using HotChocolate.Types;
using Zodpovedne.Data;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;

namespace Zodpovedne.GraphQL
{
    public class Query
    {
        // Definuje v GraphQL schématu pole 'freeMessages', které vrací seznam zpráv.
        // Díky atributům níže je toto pole extrémně "chytré".

        // [UseDbContext]: Tento atribut se stará o správu instance DbContextu.
        // Použije továrnu, kterou jsme zaregistrovali ve Startup.cs, vytvoří
        // nový DbContext, zpřístupní ho této metodě a po jejím dokončení ho správně uvolní.
        [UseDbContext(typeof(ApplicationDbContext))]

        // [UseProjection], [UseFiltering], [UseSorting]: Tyto atributy aktivují
        // middleware, který "sedí" mezi GraphQL dotazem a vaší metodou. Prozkoumají
        // argumenty dotazu (jako 'where' a 'order') a upraví IQueryable dříve,
        // než se z něj stane finální SQL dotaz.
        [UseProjection]
        [UseFiltering]
        [UseSorting]

        // Metoda, která se spustí, když klient požádá o pole 'freeMessages'.
        // Vrací IQueryable<T>, což je klíčové. Je to "předpis" na dotaz, nikoliv
        // data samotná. To umožňuje atributům výše tento předpis dále modifikovat
        // (přidat WHERE, ORDER BY...), a až poté se celý dotaz pošle do databáze.
        public IQueryable<FreeMessage> GetFreeMessages(
            // [Service]: Moderní způsob, jak si vyžádat službu zaregistrovanou
            // ve Startup.cs. V tomto případě si žádáme o náš ApplicationDbContext.
            [Service] ApplicationDbContext context)
        {
            return context.FreeMessages;
        }
    }
}