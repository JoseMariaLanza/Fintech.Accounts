using FluentAssertions;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Accounts.IntegrationTests.Infra
{
    // Fixture que inicia y destruye un RabbitMQ en Docker para la suite.
    public sealed class RabbitMqFixture : IAsyncLifetime
    {
        public RabbitMqContainer Container { get; } =
            new RabbitMqBuilder()
                .WithImage("rabbitmq:3-management")
                .WithUsername("guest")
                .WithPassword("guest")
                .Build();

        public Task InitializeAsync() => Container.StartAsync();
        public Task DisposeAsync() => Container.DisposeAsync().AsTask();

        // Helpers portables para cualquier versión del paquete:
        public string Host => Container.Hostname;
        public int Amqp => Container.GetMappedPublicPort(5672); // ⚠️ clave del fix
    }

    public sealed class RabbitMqSmokeTests : IClassFixture<RabbitMqFixture>
    {
        private readonly RabbitMqFixture _fx;
        public RabbitMqSmokeTests(RabbitMqFixture fx) => _fx = fx;

        [Fact]
        public async Task should_open_amqp_connection()
        {
            // Probar "arranque" = poder abrir conexión AMQP
            var factory = new ConnectionFactory
            {
                HostName = _fx.Host,
                Port = _fx.Amqp,
                UserName = "guest",
                Password = "guest"
            };

            // 1) Se abre la conexión (API async)
            var conn = await factory.CreateConnectionAsync();

            try
            {
                // 2) Creamos un canal (Si falla, el broker no está Ok)
                var ch = await conn.CreateChannelAsync();

                try
                {
                    ch.IsOpen.Should().BeTrue();
                }
                finally
                {
                    await ch.CloseAsync();
                    ch.Dispose();
                }
            }
            finally
            {
                await conn.CloseAsync();
                conn.Dispose();
            }
        }
    }
}
