using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;
using HotChocolate.Data; // Pro [UseDbContext]
using HotChocolate.Types; // Pro [ScopedService]

namespace Zodpovedne.GraphQL;

public class Query
{
    // Atributy, které automaticky spravují DbContext a umožňují klientovi
    // specifikovat sloupce (Projection), filtrovat (Filtering) a řadit (Sorting).
    [UseDbContext(typeof(ApplicationDbContext))]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<FreeMessage> GetFreeMessages([ScopedService] ApplicationDbContext context)
    {
        return context.FreeMessages;
    }
}