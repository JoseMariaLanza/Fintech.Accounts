using Accounts.Application.Accounts.Commands;
using Accounts.Application.Common.Interfaces;
using Accounts.Domain.Accounts.Entities;
using Fintech.Shared.Events;
using Fintech.Shared.Messaging;
using FluentAssertions;
using MediatR;
using Moq;
using System.Security.Principal;

namespace Accounts.UnitTests.Application.Accounts
{
    public class TransferFundsCommandHandlerTests
    {
        // Mocks de las dependencias del handler
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IOutboxMessageRepository> _outboxRepositoryMock;
        private readonly Mock<IOutboxDispatcher> _outboxDispatcher;

        public TransferFundsCommandHandlerTests()
        {
            // Creamos los mocks una sola vez por clase de test
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _outboxRepositoryMock = new Mock<IOutboxMessageRepository>();
            _outboxDispatcher = new Mock<IOutboxDispatcher>();
        }

        [Fact]
        public async Task Fails_when_insufficient_balance_and_does_not_save()
        {
            var from = new Account(Guid.NewGuid(), "A", 50m);
            var to = new Account(Guid.NewGuid(), "B", 0m);

            _accountRepositoryMock
                .Setup(r => r.GetByIdAsync(from.Id))
                .ReturnsAsync(from);

            _accountRepositoryMock
                .Setup(r => r.GetByIdAsync(to.Id))
                .ReturnsAsync(to);

            //var handler = new TransferFundsCommandHandler(repo, /* mapper */ null!, /* bus (P2) */ null!);
            var handler = new TransferFundsCommandHandler(
                _accountRepositoryMock.Object,
                _outboxRepositoryMock.Object,
                _outboxDispatcher.Object
                );

            var cmd = new TransferFundsCommand();
            cmd.FromAccountId = from.Id;
            cmd.ToAccountId = to.Id;
            cmd.Amount = 100m;

            Func<Task> act = () => handler.Handle(cmd, default);

            await act.Should().ThrowAsync<InvalidOperationException>();
            // Assert extra: nunca se llamó a SaveChangesAsync
            _accountRepositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);

            // Y nunca se encoló un mensaje en el outbox
            _outboxRepositoryMock.Verify(
                r => r.EnqueueAsync(
                    It.IsAny<TransferCompleted>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Fails_when_any_account_is_missing_and_does_not_save()
        {

            var existing = new Account(Guid.NewGuid(), "A", 100m);

            _accountRepositoryMock
                .Setup(r => r.GetByIdAsync(existing.Id))
                .ReturnsAsync(existing);

            _accountRepositoryMock
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            var missingId = Guid.NewGuid();

            var handler = new TransferFundsCommandHandler(
                _accountRepositoryMock.Object,
                _outboxRepositoryMock.Object,
                _outboxDispatcher.Object
                );

            var cmd = new TransferFundsCommand
            {
                FromAccountId = existing.Id,
                ToAccountId = missingId,
                Amount = 10m
            };

            Func<Task> act = () => handler.Handle(cmd, default);

            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("Some of the specified accounts does not exists.");

            _accountRepositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        //public async Task Succeeds_when_enough_balance_and_saves_once()
        //{
        //    var account = Substitute.For<IAccountRepository>();
        //    var outbox = Substitute.For<IOutboxMessageRepository>();

        //    var from = new Account(Guid.NewGuid(), "A", 150m);
        //    var to = new Account(Guid.NewGuid(), "B", 10m);

        //    account.GetByIdAsync(from.Id).Returns(from);
        //    account.GetByIdAsync(to.Id).Returns(to);

        //    var handler = new TransferFundsCommandHandler(account, outbox);

        //    var cmd = new TransferFundsCommand
        //    {
        //        FromAccountId = from.Id,
        //        ToAccountId = to.Id,
        //        Amount = 50m
        //    };

        //    var result = await handler.Handle(cmd, default);

        //    result.Should().Be(Unit.Value);
        //    from.Balance.Should().Be(100m);
        //    to.Balance.Should().Be(60m);
        //    await account.Received(1).SaveChangesAsync();
        //}
        public async Task Succeeds_when_enough_balance_and_saves_once()
        {
            // -------- Arrange --------

            // IDs fijos para las cuentas (los mismos que usás en otros tests)
            var fromId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var toId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

            // Cuentas con saldo suficiente
            var from = new Account(fromId, "Alice", 1000m);
            var to = new Account(toId, "Bob", 500m);

            // El repositorio devuelve esas cuentas cuando se las pide por ID
            _accountRepositoryMock
                .Setup(r => r.GetByIdAsync(fromId))
                .ReturnsAsync(from);

            _accountRepositoryMock
                .Setup(r => r.GetByIdAsync(toId))
                .ReturnsAsync(to);

            // Simulamos que EF realmente guardó cambios (por ejemplo, 3 entidades)
            // Esto hace que saved > 0 en el handler y NO lance InvalidOperationException.
            _accountRepositoryMock
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            OutboxMessage? capturedOutbox = null;

            // Simulamos que la encolada en el Outbox funciona y no lanza excepciones
            _outboxRepositoryMock
                .Setup(r => r.EnqueueAsync(
                    It.IsAny<TransferCompleted>(),        // evento que se pasa
                    It.IsAny<string>(),                   // routingKey (ej: "transfer.completed")
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((TransferCompleted evt, string routingKey, CancellationToken _) =>
                {
                    // Creamos el mensaje usando tu lógica de dominio
                    capturedOutbox = OutboxMessage.Create(evt, routingKey);
                    return capturedOutbox;
                });

            // Creamos el handler con los mocks como dependencias
            var handler = new TransferFundsCommandHandler(
                _accountRepositoryMock.Object,
                _outboxRepositoryMock.Object,
                _outboxDispatcher.Object
                );

            // Comando de prueba (mismos datos que las cuentas sembradas)
            var command = new TransferFundsCommand
            {
                FromAccountId = fromId,
                ToAccountId = toId,
                Amount = 50m
            };

            // -------- Act --------

            // Ejecutamos el handler
            await handler.Handle(command, CancellationToken.None);

            // -------- Assert --------

            // 1) Los saldos se actualizaron correctamente
            from.Balance.Should().Be(950m);   // 1000 - 50
            to.Balance.Should().Be(550m);     // 500 + 50

            // 2) SaveChangesAsync se llamó una sola vez
            _accountRepositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            // 3) Se encoló un mensaje en el Outbox con la routingKey correcta
            _outboxRepositoryMock.Verify(
                r => r.EnqueueAsync(
                    It.IsAny<TransferCompleted>(),
                    "transfer.completed",
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // 4) Se llamó al dispatcher con el MISMO OutboxMessage
            capturedOutbox.Should().NotBeNull();  // sanity check

            _outboxDispatcher.Verify(
                d => d.DispatchPendingAsync(
                    capturedOutbox!,                // el mismo que devolvió EnqueueAsync
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

    }
}
