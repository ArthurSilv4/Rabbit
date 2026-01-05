using RabbitMQ.Client;
using System.Transactions;

namespace ValidationService
{
    public class RabbitMqSetup
    {
        public static async Task ConfigurarAsync(IChannel channel)
        {
            Console.WriteLine("Configurando RabbitMQ...\n");

            await channel.ExchangeDeclareAsync(
                exchange: "access.events",
                type: ExchangeType.Topic,
                durable: true
            );
            Console.WriteLine("Exchange principal criado");

            await channel.ExchangeDeclareAsync(
                exchange: "access.dlx",
                type: ExchangeType.Direct,
                durable: true
            );
            Console.WriteLine("Dead Letter Exchange criado");

            await channel.QueueDeclareAsync(
                queue: "access.processing.dlq",
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            await channel.QueueBindAsync(
                queue: "access.processing.dlq",
                exchange: "access.dlx",
                routingKey: "access.processing.dead"
            );
            Console.WriteLine("Dead Letter Queue criada");

            var mainArgs = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "access.dlx" },
                { "x-dead-letter-routing-key", "access.processing.dead" }
            };

            await channel.QueueDeclareAsync(
                queue: "access.processing",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: mainArgs
            );

            await channel.QueueBindAsync(
                queue: "access.processing",
                exchange: "access.events",
                routingKey: "access.entry.registered"
            );
            Console.WriteLine("Fila principal configurada com DLX");

            var retry1Args = new Dictionary<string, object>
            {
                { "x-message-ttl", 5000 },
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", "access.processing" }
            };

            await channel.QueueDeclareAsync(
                queue: "access.processing.retry.1",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: retry1Args
            );
            Console.WriteLine("Retry 1 (5s) criado");

            var retry2Args = new Dictionary<string, object>
            {
                { "x-message-ttl", 15000 },
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", "access.processing" }
            };

            await channel.QueueDeclareAsync(
                queue: "access.processing.retry.2",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: retry2Args
            );
            Console.WriteLine("Retry 2 (15s) criado");

            var retry3Args = new Dictionary<string, object>
            {
                { "x-message-ttl", 30000 },
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", "access.processing" }
            };

            await channel.QueueDeclareAsync(
                queue: "access.processing.retry.3",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: retry3Args
            );
            Console.WriteLine("Retry 3 (30s) criado");

            Console.WriteLine("Configuracao concluida!");
        }
    }
}
