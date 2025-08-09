// Zodpovedne.GraphQL/Mutation.cs

using HotChocolate.Data;
using HotChocolate.Types;
using Zodpovedne.Data;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;

namespace Zodpovedne.GraphQL
{
    public class Mutation
    {
        public record AddFreeMessageInput(string Nickname, string Text);

        [UseDbContext(typeof(ApplicationDbContext))]
        public async Task<FreeMessage> AddFreeMessageAsync(
            AddFreeMessageInput input,
            [ScopedService] ApplicationDbContext context)
        {
            var newMessage = new FreeMessage
            {
                Nickname = input.Nickname,
                Text = input.Text,
                CreatedUtc = DateTime.UtcNow
            };

            context.FreeMessages.Add(newMessage);
            await context.SaveChangesAsync();

            return newMessage;
        }
    }
}