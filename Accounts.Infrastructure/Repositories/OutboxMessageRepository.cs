using Accounts.Application.Common.Interfaces;
using Accounts.Domain.Accounts.Entities;
using Accounts.Infrastructure.Persistence;

namespace Accounts.Infrastructure.Repositories
{
    /// <summary>
    /// Implementación del repositorio de Outbox.
    /// Convierte un evento arbitrario en una fila OutboxMessage y la agrega al DbContext.
    /// NO llama a SaveChanges: eso lo controla la capa Application.
    /// </summary>
    public sealed class OutboxMessageRepository : IOutboxMessageRepository
    {
        private readonly AppDbContext _appDbContext;

        public OutboxMessageRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<OutboxMessage> EnqueueAsync<T>(T @event, string routingKey, CancellationToken ct = default)
        {
            // Se crea la entidad OutboxMessage (usa tu modelo de dominio)
            var message = OutboxMessage.Create(@event, routingKey);

            // Solo agregamos al DbContext, sin SaveChanges
            await _appDbContext.OutboxMessages.AddAsync(message, ct);

            return message;
        }
    }
}
