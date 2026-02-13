using Fintech.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;

namespace Accounts.Infrastructure.Messaging
{
    /// <summary>
    /// Opciones de configuración para RabbitMQ.
    /// Se van a mapear con variables de entorno:
    /// RABBITMQ__HOST, RABBITMQ__USER, RABBITMQ__PASS, RABBITMQ__VHOST.
    /// </summary>
    public sealed class RabbitMqOptions
    {
        public string Host { get; set; } = "localhost";
        public string User { get; set; } = "guest";
        public string Pass { get; set; } = "guest";
        public string VHost { get; set; } = "/";
        public string ExchangeName { get; set; } = "fintech.events";
    }

    /// <summary>
    /// Implementación de IEventBus que publica eventos en RabbitMQ.
    /// Solo se usa para PublishAsync; Subscribe no se soporta en este adapter.
    /// </summary>
    public sealed class RabbitMqEventBus : IEventBus, IDisposable
    {
        private readonly RabbitMqOptions _options;
        private readonly ILogger<RabbitMqEventBus> _logger;

        // Factory con la configuración de conexión
        private readonly ConnectionFactory _factory;

        // Conexión y canal se crean de forma perezosa
        private IConnection? _connection;
        private IChannel? _channel;

        // Para evitar crear la conexión/canal en paralelo desde varios threads
        private readonly SemaphoreSlim _sync = new(1, 1);

        public RabbitMqEventBus(IOptions<RabbitMqOptions> options, ILogger<RabbitMqEventBus> logger)
        {
            _options = options.Value;
            _logger = logger;

            // Solo configuración de la factory
            _factory = new ConnectionFactory
            {
                HostName = _options.Host,
                UserName = _options.User,
                Password = _options.Pass,
                VirtualHost = _options.VHost,

                // Habilitación de reconexión automática (buena práctica)
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true
            };
        }

        /// <summary>
        /// Devuelve un canal abierto, creando conexión/canal si hace falta.
        /// Usa solo métodos async del cliente v7 (CreateConnectionAsync, CreateChannelAsync, ExchangeDeclareAsync).
        /// </summary>
        private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken ct)
        {
            // Si ya tenemos canal abierto, lo devolvemos
            if (_channel is { IsOpen: true })
                return _channel;

            await _sync.WaitAsync(ct);
            try
            {
                // Otro thread pudo crear la conexión/canal mientras esperábamos
                if (_channel is { IsOpen: true })
                    return _channel;

                // Cerrar recursos viejos si existen
                _channel?.Dispose();
                _connection?.Dispose();

                // 1) Crear connection async
                _connection = await _factory.CreateConnectionAsync(ct);

                // Publisher confirms
                var chanelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);

                // 2) Crear channel async
                _channel = await _connection.CreateChannelAsync(chanelOptions, cancellationToken: ct);

                // 3) Declarar exchange (idempotente, topic, durable)
                await _channel.ExchangeDeclareAsync(
                    exchange: _options.ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: ct);

                _logger.LogInformation(
                    "RabbitMqEventBus connected to {Host} vhost {VHost}, exchange {Exchange}",
                    _options.Host, _options.VHost, _options.ExchangeName);

                return _channel;
            }
            finally
            {
                _sync.Release();
            }
        }

        /// <summary>
        /// Publica un evento en RabbitMQ usando BasicPublishAsync.
        /// </summary>
        public async Task PublishAsync<T>(T @event, string? routingKey = null, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return;

            // Se obtiene (o crea) el canal
            var channel = await GetOrCreateChannelAsync(ct);

            // Si no viene routingKey, se usa una por defecto
            var effectiveRoutingKey = string.IsNullOrWhiteSpace(routingKey)
                ? "generic.events"
                : routingKey;

            // Serializar el evento a JSON
            var json = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(json);

            // Propiedades del mensaje
            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,       // 2 = Persistente
                Type = typeof(T).Name                          // tipo lógico del mensaje
            };

            try
            {
                // Publicación async en el exchange configurado
                // Con publisherConfirmationsEnabled + tracking, BasicPublishAsync:
                //   - completa OK cuando el broker confirma el mensaje (ack),
                //   - lanza excepción si hay un problema grave (nack/returned/disconnected).
                await channel.BasicPublishAsync(
                    exchange: _options.ExchangeName,
                    routingKey: effectiveRoutingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: body,
                    cancellationToken: ct);

                _logger.LogInformation(
                    "Event published in RabbitMQ: Type {EventType}, RoutingKey {RoutingKey}",
                    typeof(T).Name,
                    effectiveRoutingKey);
            }
            catch (AlreadyClosedException ex)
            {
                _logger.LogInformation(ex,
                    "Error publishing event {EventType} (channel or connection closed). RoutingKey={RoutingKey}",
                    typeof(T).Name,
                    effectiveRoutingKey);
                throw; // El dispatcher/outbox marcará como Failed al capturar esta excepción
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error publishing event {EventType} in RabbitMQ. RoutingKey={RoutingKey}",
                    typeof(T).Name,
                    effectiveRoutingKey);
                throw;
            }
        }

        /// <summary>
        /// En este adapter no soportamos Subscribe.
        /// El consumo se hará desde Notifications.Consumer con su propio subscriber.
        /// </summary>
        public void Subscribe<T>(Func<T, Task> handler)
        {
            throw new NotSupportedException(
                "RabbitMqEventBus does not support Subscribe. Use a dedicated Notifications consummer");
        }

        public void Dispose()
        {
            try
            {
                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch
            {
                // No queremos tirar excepción al apagar la app
            }
        }
    }
}
