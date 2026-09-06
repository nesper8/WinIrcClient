using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WinIrcClient.Data;
using WinIrcClient.Models;
using System.IO;

namespace WinIrcClient.Services
{
    public sealed class MessageHistoryService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        public event Action? HistoryCleared;

        public MessageHistoryService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<IReadOnlyList<Message>> LoadRecentAsync(int limit = 500)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var messages = await db.Messages
                    .AsNoTracking()
                    // SQLite cannot translate DateTimeOffset ordering; IDs preserve insert order.
                    .OrderByDescending(message => message.Id)
                    .Take(limit)
                    .ToListAsync()
                    .ConfigureAwait(false);

                messages.Reverse();
                return messages;
            }
            catch
            {
                throw;
            }
        }

        public async Task SaveAsync(Message message)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Messages.Add(message);
                await db.SaveChangesAsync().ConfigureAwait(false);
            }
            catch
            {
                throw;
            }
        }

        public async Task ClearAllAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Messages.ExecuteDeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Messages.RemoveRange(db.Messages);
                await db.SaveChangesAsync().ConfigureAwait(false);
            }

            HistoryCleared?.Invoke();
        }
    }
}