using System.Text.Json;
using Accounts.Application.Common.Interfaces;          // IOutboxDispatcher
using Accounts.Domain.Accounts.Entities;              // OutboxMessage, MessageStatuses
using Accounts.Infrastructure.Persistence;            // AppDbContext
using Fintech.Shared.Events;                          // TransferCompleted
using Fintech.Shared.Messaging;                       // IEventBus
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Accounts.Infrastructure.Outbox
{
    public sealed class OutboxDispatcher : IOutboxDispatcher
    {
        private readonly AppDbContext _dbContext;
        private readonly IEventBus _eventBus;
        private readonly ILogger<OutboxDispatcher> _logger;

        public OutboxDispatcher(
            AppDbContext dbContext,
            IEventBus eventBus,
            ILogger<OutboxDispatcher> logger)
        {
            _dbContext = dbContext;
            _eventBus = eventBus;
            _logger = logger;
        }

        // *** NUEVO: despachar un mensaje concreto (desde el handler) ***
        public async Task DispatchPendingAsync(OutboxMessage message, CancellationToken ct = default)
        {
            await ProcessMessageAsync(message, ct);
            await _dbContext.SaveChangesAsync(ct); // persiste Sent / Failed
        }

        /// <summary>
        /// Procesa hasta batchSize mensajes con Status = Pending.
        /// Usado típicamente desde un Job.
        /// </summary>
        public async Task DispatchPendingAsync(
            int batchSize,
            CancellationToken ct = default)
        {
            var pendingMessages = await _dbContext.OutboxMessages
                .Where(m => m.Status == MessageStatuses.Pending)
                .OrderBy(m => m.CreatedAtUtc)
                .Take(batchSize)
                .ToListAsync(ct);

            if (pendingMessages.Count == 0)
            {
                _logger.LogDebug("OutboxDispatcher: no pending messages found.");
                return;
            }

            _logger.LogInformation(
                "OutboxDispatcher: dispatching {Count} pending messages...",
                pendingMessages.Count);

            foreach (var message in pendingMessages)
            {
                if (ct.IsCancellationRequested)
                {
                    _logger.LogWarning("OutboxDispatcher: cancellation requested, stopping loop.");
                    break;
                }

                await ProcessMessageAsync(message, ct);
            }

            await _dbContext.SaveChangesAsync(ct);
        }

        // *** NUEVO: lógica común para un mensaje (single o batch) ***
        private async Task ProcessMessageAsync(
            OutboxMessage message,
            CancellationToken ct)
        {
            try
            {
                switch (message.Type)
                {
                    case nameof(TransferCompleted):
                        await DispatchTransferCompletedAsync(message, ct);
                        break;

                    default:
                        _logger.LogError(
                            "OutboxDispatcher: unknown message type '{Type}' in OutboxMessage {Id}",
                            message.Type,
                            message.Id);

                        message.MarkAsFailed($"Unknown message type: {message.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "OutboxDispatcher: error dispatching OutboxMessage {Id}",
                    message.Id);

                message.MarkAsFailed(ex.Message);
            }
        }

        /// <summary>
        /// Deserializa un OutboxMessage de tipo TransferCompleted
        /// y lo publica en IEventBus.
        /// </summary>
        private async Task DispatchTransferCompletedAsync(
            OutboxMessage message,
            CancellationToken ct)
        {
            var evt = message.Payload
                .RootElement
                .Deserialize<TransferCompleted>();

            if (evt is null)
            {
                _logger.LogError(
                    "OutboxDispatcher: deserialization returned null for TransferCompleted in OutboxMessage {Id}",
                    message.Id);

                message.MarkAsFailed("Deserialization returned null for TransferCompleted");
                return;
            }

            await _eventBus.PublishAsync(
                evt,
                message.RoutingKey,   // ej: "transfer.completed"
                ct);

            message.MarkAsSent();
        }
    }
}
