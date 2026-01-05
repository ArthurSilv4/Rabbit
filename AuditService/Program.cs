using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("AUDIT SERVICE");

        var factory = new ConnectionFactory { HostName = "localhost" };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync("access.events", ExchangeType.Topic, durable: true);

        var queue = await channel.QueueDeclareAsync();
        await channel.QueueBindAsync(queue.QueueName, "access.events", "#");

        Console.WriteLine("Audit conectado - registrando tudo\n");

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var routingKey = ea.RoutingKey;
                var body = ea.Body.ToArray();
                var message = System.Text.Encoding.UTF8.GetString(body);

                var logEntry = new
                {
                    timestamp = DateTime.Now,
                    routingKey = routingKey,
                    payload = JsonSerializer.Deserialize<object>(message)
                };

                var logJson = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = true });

                var fileName = $"audit_{DateTime.Now:yyyy-MM-dd}.json";
                await File.AppendAllTextAsync(fileName, logJson + ",\n");

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {routingKey} registrado");

                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
                await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
            }
        };

        await channel.BasicConsumeAsync(queue.QueueName, autoAck: false, consumer: consumer);

        Console.ReadLine();
    }
}