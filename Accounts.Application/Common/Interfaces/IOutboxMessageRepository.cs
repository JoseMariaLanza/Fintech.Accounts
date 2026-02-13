using Accounts.Domain.Accounts.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Accounts.Application.Common.Interfaces
{
    /// <summary>
    /// Puerto de Outbox: permite encolar mensajes para publicación asíncrona.
    /// No realiza SaveChanges; solo agrega las filas de Outbox al contexto.
    /// </summary>
    public interface IOutboxMessageRepository
    {
        /// <summary>
        /// Encola un evento de integración para ser publicado luego.
        /// </summary>
        /// <typeparam name="T">Tipo del evento (por ejemplo, TransferCompleted)</typeparam>
        /// <param name="event">Instancia del evento a encolar.</param>
        /// <param name="routingKey">RoutingKey que se usará al publicar en RabbitMQ.</param>
        Task<OutboxMessage> EnqueueAsync<T>(T @event, string routingKey, CancellationToken ct = default);
    }
}
