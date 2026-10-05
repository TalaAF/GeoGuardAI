namespace AeroCompliance.Contracts.Messaging;

/// <summary>
/// Well-known RabbitMQ topology names shared by publishers (IngestionService) and
/// consumers (ApiService and future workers). Keeping these in one place avoids
/// magic strings drifting between services.
/// </summary>
public static class MessagingConstants
{
    public const string ProcessingExchange = "aerocompliance.processing";
    public const string ProcessingRequestRoutingKey = "image.processing.requested.v1";
    public const string AnalysisStartedRoutingKey = "image.analysis.started.v1";
    public const string AnalysisCompletedRoutingKey = "image.analysis.completed.v1";
    public const string AnalysisFailedRoutingKey = "image.analysis.failed.v1";
    public const string AiProcessingQueue = "aerocompliance.ai.image-processing.v1";
    public const string ProcessingResultsQueue = "aerocompliance.processing.image-analysis-results.v1";
    public const string AiProcessingDeadLetterQueue = "aerocompliance.ai.image-processing.dlq.v1";
    public const string ProcessingResultsDeadLetterQueue = "aerocompliance.processing.image-analysis-results.dlq.v1";
    /// <summary>Topic exchange for drone-ingestion domain events.</summary>
    public const string IngestionExchange = "aerocompliance.ingestion";

    /// <summary>Routing key for the <see cref="Messages.ImageUploaded"/> event.</summary>
    public const string ImageUploadedRoutingKey = "image.uploaded";

    /// <summary>Durable queue that receives <see cref="Messages.ImageUploaded"/> events.</summary>
    public const string ImageUploadedQueue = "aerocompliance.image-uploaded";
}
