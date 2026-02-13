using Accounts.Application.Accounts.Commands;
using Accounts.Application.Common.Interfaces;
using Accounts.Domain.Accounts.Entities;
using Fintech.Shared.Events;
using Fintech.Shared.Messaging;
using FluentAssertions;
using Moq;

namespace Accounts.UnitTests.Application.Accounts
{
    public class TransferFundsCommandHandler_PublishesEventTests
    {
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IOutboxMessageRepository> _outboxRepositoryMock;
        private readonly Mock<IOutboxDispatcher> _outboxDispatcher;

        public TransferFundsCommandHandler_PublishesEventTests()
        {
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _outboxRepositoryMock = new Mock<IOutboxMessageRepository>();
            _outboxDispatcher = new Mock<IOutboxDispatcher>();
        }

        [Fact]
        public async Task On_success_SaveChanges_Should_enqueue_TransferCompleted_in_outbox()
        {
            // Arrange
            var from = new Account(Guid.NewGuid(), "A", 1000m);
            var to = new Account(Guid.NewGuid(), "B", 0m);

            _accountRepositoryMock
                .Setup(r => r.GetByIdAsync(from.Id))
                .ReturnsAsync(from);

            _accountRepositoryMock
                .Setup(r => r.GetByIdAsync(to.Id))
                .ReturnsAsync(to);

            // Simulamos que EF guardó cambios (saved > 0)
            _accountRepositoryMock
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // *** CAMBIO: ahora EnqueueAsync devuelve OutboxMessage y
            // aprovechamos para capturar:
            //  - el evento TransferCompleted
            //  - la routingKey
            //  - el OutboxMessage creado (para verificar el dispatcher)
            TransferCompleted? capturedEvent = null;
            string? capturedRoutingKey = null;
            OutboxMessage? capturedOutbox = null;      // *** CAMBIO

            _outboxRepositoryMock
                .Setup(r => r.EnqueueAsync(
                    It.IsAny<TransferCompleted>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((TransferCompleted evt, string routingKey, CancellationToken _) =>
                {
                    capturedEvent = evt;                       // *** CAMBIO
                    capturedRoutingKey = routingKey;                // *** CAMBIO
                    capturedOutbox = OutboxMessage.Create(evt, routingKey); // *** CAMBIO
                    return capturedOutbox!;
                });

            var handler = new TransferFundsCommandHandler(
                _accountRepositoryMock.Object,
                _outboxRepositoryMock.Object,
                _outboxDispatcher.Object
                );

            var cmd = new TransferFundsCommand
            {
                FromAccountId = from.Id,
                ToAccountId = to.Id,
                Amount = 100m
            };

            // Act
            await handler.Handle(cmd, default);

            // Assert 1: se encoló en el outbox UNA vez
            _outboxRepositoryMock.Verify(
                r => r.EnqueueAsync(
                    It.IsAny<TransferCompleted>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Assert 2: se guardaron cambios una sola vez
            _accountRepositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            // Assert 3: el evento capturado tiene los datos correctos
            capturedEvent.Should().NotBeNull();
            capturedEvent!.Amount.Should().Be(100m);
            capturedEvent.FromAccountId.Should().Be(from.Id);
            capturedEvent.ToAccountId.Should().Be(to.Id);

            // Assert 4: la routingKey es la esperada
            capturedRoutingKey.Should().Be("transfer.completed");

            capturedOutbox.Should().NotBeNull();

            _outboxDispatcher.Verify(
                d => d.DispatchPendingAsync(
                    capturedOutbox!,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
