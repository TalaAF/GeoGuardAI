using AeroCompliance.Contracts.Messaging.Messages;

namespace AeroCompliance.IngestionService.Messaging;

/// <summary>
/// Publishes drone-ingestion domain events to RabbitMQ. Epic 1: connection + topology + publish
/// plumbing only; the ingestion pipeline that produces these events is built in a later epic.
/// </summary>
public interface IImageEventPublisher
{
    Task PublishImageUploadedAsync(ImageUploaded message, CancellationToken cancellationToken = default);
}
