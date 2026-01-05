using RabbitMQ.Client;

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

        Console.WriteLine("Gateway conectado ao RabbitMQ");

        for (int i = 0; i < 500; i++)
        {
            Console.WriteLine($"Solicitando Entrada {i + 1}/500...");
            await Task.Delay(1000);

            var caminhao = new Caminhao
            {
                Placa = $"ABC-{i + 1}",
                Status = "Em transito",
                Modelo = $"Volvo {i + 1}",
                CapacidadeCarga = 30000.0
            };

            string message = System.Text.Json.JsonSerializer.Serialize(caminhao);
            var body = System.Text.Encoding.UTF8.GetBytes(message);

            var props = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(
                exchange: "access.events",
                routingKey: "access.entry.registered",
                mandatory: true,
                basicProperties: props,
                body: body
            );


            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Mensagem {i + 1} publicada no RabbitMQ");
            Console.ResetColor();
        }

        Console.ReadLine();
    }
}