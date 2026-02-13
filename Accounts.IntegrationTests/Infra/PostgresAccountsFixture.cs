using Accounts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Accounts.IntegrationTests.Infra
{
    public sealed class PostgresAccountsFixture : IAsyncLifetime
    {
        public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("accountsdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        public string ConnectionsString => Container.GetConnectionString();

        public async Task InitializeAsync()
        {
            await Container.StartAsync();

            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionsString)
                .Options;

            await using var db = new AppDbContext(opts);
            await db.Database.MigrateAsync();
        }

        public Task DisposeAsync() => Container.DisposeAsync().AsTask();
    }
}
