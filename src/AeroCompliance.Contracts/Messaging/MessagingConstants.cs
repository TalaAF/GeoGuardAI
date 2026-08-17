namespace AeroCompliance.Contracts.Messaging;

/// <summary>
/// Well-known RabbitMQ topology names shared by publishers (IngestionService) and
/// consumers (ApiService and future workers). Keeping these in one place avoids
/// magic strings drifting between services.
/// </summary>
public static class MessagingConstants
{
    /// <summary>Topic exchange for drone-ingestion domain events.</summary>
    public const string IngestionExchange = "aerocompliance.ingestion";

    /// <summary>Routing key for the <see cref="Messages.ImageUploaded"/> event.</summary>
    public const string ImageUploadedRoutingKey = "image.uploaded";

    /// <summary>Durable queue that receives <see cref="Messages.ImageUploaded"/> events.</summary>
    public const string ImageUploadedQueue = "aerocompliance.image-uploaded";
}
