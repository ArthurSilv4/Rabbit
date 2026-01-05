using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class Caminhao
{
    public string Placa { get; set; }
    public string Status { get; set; }
    public string Modelo { get; set; }
    public double CapacidadeCarga { get; set; }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchange: "access.events", type: ExchangeType.Topic, durable: true, autoDelete: false);

        Console.WriteLine("Panel conectado ao RabbitMQ");

        var queue = await channel.QueueDeclareAsync();
        await channel.QueueBindAsync(queue.QueueName, "access.events", "access.entry.validated");

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = System.Text.Encoding.UTF8.GetString(body);
            var caminhao = System.Text.Json.JsonSerializer.Deserialize<Caminhao>(message);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Panel] Nova entrada registrada: {caminhao.Placa}");
            Console.ResetColor();

            await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
        };

        await channel.BasicConsumeAsync(queue: queue.QueueName, autoAck: false, consumer: consumer);

        Console.ReadLine();
    }
}