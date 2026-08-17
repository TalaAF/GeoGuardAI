using AeroCompliance.Contracts.Messaging;
using RabbitMQ.Client;

namespace AeroCompliance.IngestionService.Messaging;

/// <summary>
/// Startup background worker that declares the ingestion RabbitMQ topology once the broker is
/// reachable, so publishers/consumers don't race the first message. Epic 1: topology only —
/// no message consumption yet.
/// </summary>
public sealed class IngestionTopologyInitializer(
    IConnection connection,
    ILogger<IngestionTopologyInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.ExchangeDeclareAsync(
                exchange: MessagingConstants.IngestionExchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: MessagingConstants.ImageUploadedQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: MessagingConstants.ImageUploadedQueue,
                exchange: MessagingConstants.IngestionExchange,
                routingKey: MessagingConstants.ImageUploadedRoutingKey,
                cancellationToken: stoppingToken);

            logger.LogInformation(
                "Declared ingestion topology: exchange '{Exchange}', queue '{Queue}'",
                MessagingConstants.IngestionExchange, MessagingConstants.ImageUploadedQueue);
        }
        catch (OperationCanceledException)
        {
            // Normal on shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to declare ingestion RabbitMQ topology at startup.");
        }
    }
}
