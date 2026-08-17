using AeroCompliance.Contracts.Messaging.Messages;
using AeroCompliance.IngestionService.Ingestion;
using AeroCompliance.IngestionService.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks, service discovery, resilience.
builder.AddServiceDefaults();

// RabbitMQ client from the "messaging" Aspire resource — registers a shared IConnection.
builder.AddRabbitMQClient(connectionName: "messaging");

// Ingestion messaging plumbing.
builder.Services.AddSingleton<IImageEventPublisher, RabbitMqImageEventPublisher>();
builder.Services.AddHostedService<IngestionTopologyInitializer>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Aspire health/liveness endpoints (/health, /alive).
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Minimal ingestion endpoint skeleton: records image metadata and publishes an ImageUploaded
// event. Epic 1: no multipart file receipt or MinIO storage yet.
app.MapPost("/api/ingestion/images", async (
    ImageUploadRequest request,
    IImageEventPublisher publisher,
    CancellationToken cancellationToken) =>
{
    var imageId = Guid.NewGuid();

    var evt = new ImageUploaded(
        ImageId: imageId,
        FlightId: request.FlightId,
        StoragePath: request.StoragePath,
        Sha256Hash: request.Sha256Hash,
        CapturedAt: request.CapturedAt,
        Latitude: request.Latitude,
        Longitude: request.Longitude,
        UploadedAt: DateTimeOffset.UtcNow);

    await publisher.PublishImageUploadedAsync(evt, cancellationToken);

    return Results.Accepted($"/api/ingestion/images/{imageId}", new { imageId });
})
.WithName("IngestDroneImage")
.WithTags("Ingestion");

app.Run();
