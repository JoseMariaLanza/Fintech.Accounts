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
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IOutboxMessageRepository> _outboxRepositoryMock;
        private readonly Mock<IOutboxDispatcher> _outboxDispatcher;

        public TransferFundsCommandHandlerTests()
        {
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
            _accountRepositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);

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
        public async Task Succeeds_when_enough_balance_and_saves_once()
        {
            // -------- Arrange --------

            var fromId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var toId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

            var from = new Account(fromId, "Alice", 1000m);
            var to = new Account(toId, "Bob", 500m);

            _accountRepositoryMock
                .Setup(r => r.GetByIdAsync(fromId))
                .ReturnsAsync(from);

            _accountRepositoryMock
                .Setup(r => r.GetByIdAsync(toId))
                .ReturnsAsync(to);

            _accountRepositoryMock
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            OutboxMessage? capturedOutbox = null;

            _outboxRepositoryMock
                .Setup(r => r.EnqueueAsync(
                    It.IsAny<TransferCompleted>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((TransferCompleted evt, string routingKey, CancellationToken _) =>
                {
                    capturedOutbox = OutboxMessage.Create(evt, routingKey);
                    return capturedOutbox;
                });

            var handler = new TransferFundsCommandHandler(
                _accountRepositoryMock.Object,
                _outboxRepositoryMock.Object,
                _outboxDispatcher.Object
                );

            var command = new TransferFundsCommand
            {
                FromAccountId = fromId,
                ToAccountId = toId,
                Amount = 50m
            };

            // -------- Act --------

            await handler.Handle(command, CancellationToken.None);

            // -------- Assert --------

            from.Balance.Should().Be(950m);   // 1000 - 50
            to.Balance.Should().Be(550m);     // 500 + 50

            _accountRepositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            _outboxRepositoryMock.Verify(
                r => r.EnqueueAsync(
                    It.IsAny<TransferCompleted>(),
                    "transfer.completed",
                    It.IsAny<CancellationToken>()),
                Times.Once);

            capturedOutbox.Should().NotBeNull();  // sanity check

            _outboxDispatcher.Verify(
                d => d.DispatchPendingAsync(
                    capturedOutbox!,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

    }
}
