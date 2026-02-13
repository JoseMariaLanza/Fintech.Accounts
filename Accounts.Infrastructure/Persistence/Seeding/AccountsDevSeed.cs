using Accounts.Domain.Accounts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accounts.Infrastructure.Persistence.Seeding
{
    /// Seeding de desarrollo (idempotente).
    public static class AccountsDevSeed
    {
        public static async Task RunAsync(AppDbContext db, CancellationToken ct)
        {
            // Idempotente: si ya hay datos, no duplica
            if (!await db.Accounts.AnyAsync(ct))
            {
                db.Accounts.AddRange(
                    new Account(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Alice", 1000),
                    new Account(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Bob", 500)
                );
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
