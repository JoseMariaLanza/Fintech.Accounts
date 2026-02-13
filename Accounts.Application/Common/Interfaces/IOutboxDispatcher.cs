using Accounts.Domain.Accounts.Entities;

namespace Accounts.Application.Common.Interfaces
{
    /// <summary>
    /// Servicio que sabe tomar mensajes de la tabla Outbox
    /// y despacharlos al bus de eventos (IEventBus),
    /// actualizando el estado (Sent / Failed).
    /// 
    /// No sabe nada de RabbitMQ directamente, solo usa IEventBus.
    /// </summary>
    public interface IOutboxDispatcher
    {
        /// <summary>
        /// Procesa un batch de mensajes de outbox pendientes.
        /// 
        /// batchSize: límite de mensajes a procesar en esta ejecución.
        /// ct: token de cancelación (para jobs/hosted services).
        /// </summary>
        Task DispatchPendingAsync(int batchSize, CancellationToken ct = default);

        Task DispatchPendingAsync(OutboxMessage outboxMessage, CancellationToken ct = default);
    }
}
