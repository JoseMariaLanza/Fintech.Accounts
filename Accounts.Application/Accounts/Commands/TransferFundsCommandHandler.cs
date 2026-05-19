using Accounts.Application.Common.Interfaces;
using Fintech.Shared.Events;
using Fintech.Shared.Messaging;
using MediatR;

namespace Accounts.Application.Accounts.Commands
{
    public sealed class TransferFundsCommandHandler : IRequestHandler<TransferFundsCommand, Unit>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IOutboxMessageRepository _outboxMessageRepository;
        private readonly IOutboxDispatcher _outboxDispatcher;

        public TransferFundsCommandHandler(
            IAccountRepository accountRepository,
            IOutboxMessageRepository outboxMessageRepository,
            IOutboxDispatcher outboxDispatcher)
        {
            _accountRepository = accountRepository;
            _outboxMessageRepository = outboxMessageRepository;
            _outboxDispatcher = outboxDispatcher;
        }

        public async Task<Unit> Handle(TransferFundsCommand request, CancellationToken ct)
        {
            var from = await _accountRepository.GetByIdAsync(request.FromAccountId);
            var to = await _accountRepository.GetByIdAsync(request.ToAccountId);

            if (from is null || to is null)
                throw new KeyNotFoundException("Some of the specified accounts does not exists.");

            from.Debit(request.Amount);
            to.Credit(request.Amount);

            var evt = new TransferCompleted(
                TransferId: Guid.NewGuid(),
                FromAccountId: from.Id,
                ToAccountId: to.Id,
                Amount: request.Amount,
                OccurredAt: DateTime.UtcNow
            );

            var outboxMessage = await _outboxMessageRepository.EnqueueAsync(
                evt,
                routingKey: "transfer.completed",
                ct);

            var saved = await _accountRepository.SaveChangesAsync(ct);
            if (saved <= 0)
            {
                throw new InvalidOperationException("No changes were saved in the transfer operation.");
            }

            await _outboxDispatcher.DispatchPendingAsync(
                outboxMessage,
                ct: ct);

            return Unit.Value;
        }
    }
}
