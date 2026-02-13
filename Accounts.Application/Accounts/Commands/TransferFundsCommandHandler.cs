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

            // 3) Armar el evento de integración
            var evt = new TransferCompleted(
                TransferId: Guid.NewGuid(),
                FromAccountId: from.Id,
                ToAccountId: to.Id,
                Amount: request.Amount,
                OccurredAt: DateTime.UtcNow
            );

            // 4) Encolar el evento en el Outbox (NO se guarda aún en la DB)
            var outboxMessage = await _outboxMessageRepository.EnqueueAsync(
                evt,
                routingKey: "transfer.completed",
                ct);

            // 5) Guardar TODO junto (cuentas + outbox) en una sola transacción
            var saved = await _accountRepository.SaveChangesAsync(ct);
            if (saved <= 0)
            {
                // Opcional: lanzar excepción o log crítico si querés asegurarte
                // de que algo se haya persistido.
                throw new InvalidOperationException("No changes were saved in the transfer operation.");
            }

            //// 6) Intentar publicar el evento en RabbitMQ
            //try
            //{
            //    await _eventBus.PublishAsync(evt, routingKey: "transfer.completed", ct);
            //    // 7) Si se publicó bien, marcar OutboxMessage como Sent
            //    outboxMessage.MarkAsSent();
            //    await _accountRepository.SaveChangesAsync(ct);
            //}
            //catch (Exception ex)
            //{
            //    // 8) Si falló el publish, marcamos OutboxMessage como Failed
            //    //    (para que el job de reintento pueda verlo)
            //    outboxMessage.MarkAsFailed(ex.Message); // *** CAMBIO: tu método de dominio

            //    // Persistimos el estado Failed y NO re-lanzamos la excepción,
            //    // para no romper la operación de negocio (la transferencia).
            //    await _accountRepository.SaveChangesAsync(ct);

            //}

            await _outboxDispatcher.DispatchPendingAsync(
                outboxMessage,
                ct: ct);

            return Unit.Value;
        }
    }
}
