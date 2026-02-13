using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Accounts.Domain.Accounts.Entities
{
    public enum MessageStatuses
    {
        Pending,
        Sent,
        Failed
    }

    public class OutboxMessage
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Type { get; set; } = default!;
        public string RoutingKey { get; set; } = default!;
        public JsonDocument Payload { get; private set; } = default!;
        public DateTime CreatedAtUtc { get; private set; }
        public MessageStatuses Status { get; set; }
        public string? LastError { get; set; }
        public DateTime? SentAtUtc { get; private set; } = null;
        public int RetryCount { get; set; } = 0;

        public static OutboxMessage Create<T>(T @event, string routingKey)
        {
            var typeName = typeof(T).Name;

            var json = JsonSerializer.Serialize(@event);
            var payload = JsonDocument.Parse(json);

            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = typeName,
                RoutingKey = routingKey,
                Payload = payload,
                CreatedAtUtc = DateTime.UtcNow,
                Status = MessageStatuses.Pending,
                RetryCount = 0,
                LastError = null,
                SentAtUtc = null
            };

        }

        public void MarkAsSent()
        {
            Status = MessageStatuses.Sent;
            SentAtUtc = DateTime.UtcNow;
            LastError = null;
        }

        public void MarkAsFailed(string error)
        {
            Status = MessageStatuses.Failed;
            LastError = error;
            RetryCount++;
        }

    }
}
