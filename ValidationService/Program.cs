using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ValidationService;
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

        await RabbitMqSetup.ConfigurarAsync(channel);

        await channel.BasicQosAsync(0, 1, false); 

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var headers = ea.BasicProperties.Headers ?? new Dictionary<string, object>();
            int retryCount = 0;

            if (headers.ContainsKey("x-retry-count"))
            {
                retryCount = Convert.ToInt32(headers["x-retry-count"]);
            }

            try
            {
                var body = ea.Body.ToArray();
                var message = System.Text.Encoding.UTF8.GetString(body);
                var caminhao = System.Text.Json.JsonSerializer.Deserialize<Caminhao>(message);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Tentativa {retryCount + 1}/3] Processando: {caminhao.Placa}");
                Console.ResetColor();

                var random = Random.Shared.Next(1, 11);
                if (random <= 3)
                {
                    throw new Exception("API instável");
                }

                await Task.Delay(2000);

                caminhao.Status = "VALIDATED";

                var validatedMessage = System.Text.Json.JsonSerializer.Serialize(caminhao);
                var props = new BasicProperties { Persistent = true };

                await channel.BasicPublishAsync(
                    exchange: "access.events",
                    routingKey: "access.entry.validated",
                    mandatory: false,
                    basicProperties: props,
                    body: System.Text.Encoding.UTF8.GetBytes(validatedMessage)
                );

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{caminhao.Placa} validado");
                Console.ResetColor();

                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Erro: {ex.Message}");
                Console.ResetColor();

                retryCount++;

                if (retryCount <= 3)
                {
                    string retryQueue = retryCount switch
                    {
                        1 => "access.processing.retry.1",
                        2 => "access.processing.retry.2",
                        3 => "access.processing.retry.3",
                        _ => null
                    };

                    if (retryQueue != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Agendando retry {retryCount}/3...");
                        Console.ResetColor();

                        headers["x-retry-count"] = retryCount;

                        var retryProps = new BasicProperties
                        {
                            Persistent = true,
                            Headers = headers
                        };

                        await channel.BasicPublishAsync(
                            exchange: "",
                            routingKey: retryQueue,
                            mandatory: false,
                            basicProperties: retryProps,
                            body: ea.Body
                        );

                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Falhou 3x - indo pra DLQ");
                        Console.ResetColor();

                        await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Limite excedido - DLQ");
                    Console.ResetColor();

                    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                }
            }
        };

        await channel.BasicConsumeAsync(queue: "access.processing", autoAck: false, consumer: consumer);

        Console.ReadLine();
    }
}