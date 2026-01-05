using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("MONITOR DE DEAD LETTER QUEUE");

        var factory = new ConnectionFactory { HostName = "localhost" };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        Console.WriteLine("Monitor conectado à DLQ\n");
        Console.WriteLine("Aguardando mensagens que falharam...\n");

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = System.Text.Encoding.UTF8.GetString(body);

                var headers = ea.BasicProperties.Headers ?? new Dictionary<string, object>();
                int retryCount = 0;
                if (headers.ContainsKey("x-retry-count"))
                {
                    retryCount = Convert.ToInt32(headers["x-retry-count"]);
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] MENSAGEM NA DLQ");
                Console.ResetColor();

                Console.WriteLine($"Tentativas: {retryCount}");
                Console.WriteLine($"Payload: {message}");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("ALERTA ENVIADO PARA EQUIPE");
                Console.ResetColor();

                await File.AppendAllTextAsync(
                    "dlq_errors.log",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Tentativas: {retryCount} | {message}\n"
                );

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Registrado em dlq_errors.log\n");
                Console.ResetColor();

                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Erro no monitor: {ex.Message}");
                Console.ResetColor();

                await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
            }
        };

        await channel.BasicConsumeAsync(
            queue: "access.processing.dlq",
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine("Pressione ENTER para encerrar...");
        Console.ReadLine();
    }
}