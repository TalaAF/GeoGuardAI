using System.Text;
using System.Text.Json;
using AeroCompliance.Contracts.Messaging;
using AeroCompliance.Contracts.Messaging.Messages;
using RabbitMQ.Client;

namespace AeroCompliance.IngestionService.Messaging;

/// <summary>
/// RabbitMQ implementation of <see cref="IImageEventPublisher"/> using the async RabbitMQ.Client 7.x API.
/// The shared <see cref="IConnection"/> is provided by the Aspire RabbitMQ client integration; a channel
/// is opened per publish (channels are cheap and not thread-safe to share).
/// </summary>
public sealed class RabbitMqImageEventPublisher(IConnection connection, ILogger<RabbitMqImageEventPublisher> logger)
    : IImageEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishImageUploadedAsync(ImageUploaded message, CancellationToken cancellationToken = default)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Idempotent topology declaration (safe to repeat): topic exchange + durable queue + binding.
        await channel.ExchangeDeclareAsync(
            exchange: MessagingConstants.IngestionExchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: MessagingConstants.ImageUploadedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: MessagingConstants.ImageUploadedQueue,
            exchange: MessagingConstants.IngestionExchange,
            routingKey: MessagingConstants.ImageUploadedRoutingKey,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = message.ImageId.ToString(),
            Type = nameof(ImageUploaded)
        };

        await channel.BasicPublishAsync(
            exchange: MessagingConstants.IngestionExchange,
            routingKey: MessagingConstants.ImageUploadedRoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Published {Event} for image {ImageId} (flight {FlightId})",
            nameof(ImageUploaded), message.ImageId, message.FlightId);
    }
}
