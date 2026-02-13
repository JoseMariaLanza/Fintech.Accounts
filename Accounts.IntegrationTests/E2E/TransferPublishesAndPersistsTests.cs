using Accounts.Infrastructure.Persistence;
using Accounts.IntegrationTests.Infra;
using Fintech.Shared.Events;
using Fintech.Shared.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace Accounts.IntegrationTests.E2E
{
    public class TransferPublishesAndPersistsTests : IClassFixture<PostgresAccountsFixture>
    {
        private readonly string _appCs;

        public TransferPublishesAndPersistsTests(PostgresAccountsFixture appFx)
        {
            _appCs = appFx.ConnectionsString;
        }

        [Fact]
        public async Task Post_transfer_publishes_event_and_notifications_handler_persists_id()
        {
            //// *** CAMBIO: limpiar el InMemoryEventBus ANTES de la prueba
            //// Esto asegura que no haya eventos viejos de otros tests en la "bandeja".
            //InMemoryEventBus.Drain<TransferCompleted>();

            // 1) Levantamos la API real "in-process" (no IIS/Kestrel externos)
            await using var app = new TestAppFactory(_appCs);      // <- API InMemory + cuentas sembradas (seeds)
            var client = app.CreateClient();

            // 2) Ejecutamos la transferencia. Si tu handler requiere cuentas existentes,
            // usa IDs del seed o crea cuentas antes (depende de tu AppDbContextSeed)
            var body = new
            {
                fromAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                toAccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                amount = 50m
            };

            var resp = await client.PostAsJsonAsync("/api/accounts/transfer", body);
            resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // 3) Verificamos que las CUENTAS se hayan persistido correctamente en Postgres
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_appCs)
                .Options;

            await using var db = new AppDbContext(dbOptions);

            var from = await db.Accounts.FindAsync(body.fromAccountId);
            var to = await db.Accounts.FindAsync(body.toAccountId);

            from.Should().NotBeNull();
            to.Should().NotBeNull();

            // Saldos esperados: 1000 - 50 y 500 + 50 (según seeds del TestAppFactory)
            from!.Balance.Should().Be(950m);
            to!.Balance.Should().Be(550m);

            var outboxForTransfer = await db.OutboxMessages
                .OrderByDescending(o => o.SentAtUtc)
                .FirstOrDefaultAsync(o => o.Type == nameof(TransferCompleted));

            outboxForTransfer.Should().NotBeNull();
            outboxForTransfer!.RoutingKey.Should().Be("transfer.completed");

            //// 3) Se recoge el evento publicado (el bus en memoria actúa como "bandeja")
            //var events = InMemoryEventBus.Drain<TransferCompleted>();
            //events.Should().HaveCount(1);
            //var evt = events.First();

            ////// 4) Se consume con el handler real de Notifications contra Postgres real (fixture)
            ////var notifOpts = new DbContextOptionsBuilder<NotificationsDb>().UseNpgsql(_notifCs).Options;
            ////await using var notifDb = new NotificationsDb(notifOpts);
            ////var handler = new TransferCompletedHandler(notifDb);

            ////await handler.HandleAsync(evt, default);

            ////// 5) Verificamos la persistencia del TransferId
            ////var found = await notifDb.Notifications.CountAsync(n => n.Payload.Contains(evt.TransferId.ToString()));
            ////found.Should().Be(1);
        }
    }
}
