using Accounts.Domain.Accounts.Entities;
using Accounts.Infrastructure.Persistence;
using Fintech.Shared.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Accounts.IntegrationTests.E2E
{
    public sealed class TestAppFactory : WebApplicationFactory<Program>
    {
        private readonly string _appCs;
        public TestAppFactory(string appConnectionString) => _appCs = appConnectionString;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                //// 1) Desregistrar hosted service que migra/seed en production
                //// (evita PendingModelChanges y dependencias del estado real)
                //var hosted = services.FirstOrDefault(d =>
                //    d.ServiceType == typeof(IHostedService) &&
                //    d.ImplementationType?.Name == "DbSeedHostedService");
                //if (hosted is not null) services.Remove(hosted);

                //// 2) Reemplazar AppDbContext -> InMemory
                //var dbOpt = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                //services.Remove(dbOpt);
                //services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("appdb-e2e"));

                //// 3) Sembrar dos cuentas con IDs conocidos
                //using var sp = services.BuildServiceProvider();
                //using var scope = sp.CreateScope();
                //var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                //db.Database.EnsureCreated();

                // 1) Quitar el DbContextOptions<AppDbContext> registrado por Program.cs
                var dbOpt = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                services.Remove(dbOpt);

                // 2) Registrar AppDbContext contra Postgres (del fixture)
                services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_appCs));

                // 3) Reemplazar IEventBus por InMemoryEventBus SOLO en tests
                var eventBusDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEventBus));
                if (eventBusDescriptor is not null)
                {
                    services.Remove(eventBusDescriptor);                  // quitamos RabbitMqEventBus
                }
                services.AddSingleton<IEventBus, InMemoryEventBus>();    // ponemos InMemoryEventBus

                // 4) (Opcional) Sembrar 2 cuentas fijas para el E2E
                using var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();

                var from = new Account(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Alice", 1000m);
                var to = new Account(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Bob", 500m);

                if (!db.Accounts.Any(x => x.Id == from.Id)) db.Accounts.Add(from);
                if (!db.Accounts.Any(x => x.Id == to.Id)) db.Accounts.Add(to);
                db.SaveChanges();
            });
        }
    }
}
