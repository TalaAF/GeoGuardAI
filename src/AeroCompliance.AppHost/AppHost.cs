// AeroCompliance — Aspire orchestrator (Epic 1: infrastructure wiring only).
//
// Resources:
//   * PostgreSQL (postgis/postgis image) + pgAdmin        — geospatial database
//   * RabbitMQ (management plugin)                          — async messaging
//   * MinIO (generic container: S3 API + web console)       — object storage
//
// All three are referenced into both the ApiService and the IngestionService.

var builder = DistributedApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// PostgreSQL + PostGIS
// ---------------------------------------------------------------------------
// Persist credentials/data across runs via parameters + a named volume, and swap the
// default postgres image for postgis/postgis so the geometry columns work out of the box.
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithImage("postgis/postgis")
    .WithImageTag("16-3.4")
    .WithDataVolume("aerocompliance-pgdata-v2")
    .WithPgAdmin(); // local inspection UI

var aeroDb = postgres.AddDatabase("aerocompliancedb");

// ---------------------------------------------------------------------------
// RabbitMQ (with the management plugin / UI)
// ---------------------------------------------------------------------------
var rabbitUser = builder.AddParameter("rabbitmq-user", secret: true);
var rabbitPassword = builder.AddParameter("rabbitmq-password", secret: true);

var rabbitmq = builder.AddRabbitMQ("messaging", rabbitUser, rabbitPassword)
    .WithDataVolume("aerocompliance-rabbitdata-v2")
    .WithManagementPlugin(); // exposes the RabbitMQ management UI

// ---------------------------------------------------------------------------
// MinIO (S3-compatible object storage) — no native Aspire component, so use a
// generic container resource. Exposes 9000 (S3 API) and 9001 (web console).
// ---------------------------------------------------------------------------
var minioRootUser = builder.AddParameter("minio-root-user", secret: true);
var minioRootPassword = builder.AddParameter("minio-root-password", secret: true);

var minio = builder.AddContainer("minio", "minio/minio")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEnvironment("MINIO_ROOT_USER", minioRootUser)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioRootPassword)
    .WithVolume("aerocompliance-miniodata", "/data")
    .WithEndpoint(port: 9000, targetPort: 9000, name: "s3", scheme: "http")
    .WithEndpoint(port: 9001, targetPort: 9001, name: "console", scheme: "http");

var minioS3Endpoint = minio.GetEndpoint("s3");

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
var apiService = builder.AddProject<Projects.AeroCompliance_ApiService>("apiservice")
    .WithReference(aeroDb).WaitFor(aeroDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    // MinIO connection details passed as configuration (generic container has no client integration).
    .WithEnvironment("MinIO__Endpoint", minioS3Endpoint)
    .WithEnvironment("MinIO__AccessKey", minioRootUser)
    .WithEnvironment("MinIO__SecretKey", minioRootPassword)
    .WithEnvironment("MinIO__Bucket", "aerocompliance-images")
    .WaitFor(minio);

builder.AddProject<Projects.AeroCompliance_ProcessingService>("processingservice")
    .WithReference(aeroDb).WaitFor(aeroDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WaitFor(apiService);

// AI Worker mode is chosen at launch, not baked in:
//   AI_MODE=yolo (default here) runs the real model from the read-only mount below;
//   set AEROCOMPLIANCE_AI_MODE=mock to fall back to the deterministic mock without a rebuild.
// The dependency profile must match: real inference needs the ultralytics/PyTorch profile.
var aiMode = Environment.GetEnvironmentVariable("AEROCOMPLIANCE_AI_MODE")?.Trim().ToLowerInvariant() is "mock"
    ? "mock"
    : "yolo";
var aiRequirements = aiMode == "yolo" ? "requirements-yolo.txt" : "requirements-geospatial.txt";

// The .pt weights are large and are never copied into the image (.dockerignore excludes models/*.pt);
// they are bind-mounted read-only from the developer's checkout instead.
var modelHostPath = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "AeroCompliance.AiWorker", "models", "best.pt"));

var aiWorker = builder.AddContainer("aiworker", "aerocompliance-ai-worker")
    .WithDockerfile("../AeroCompliance.AiWorker")
    .WithBuildArg("AI_REQUIREMENTS", aiRequirements)
    .WithReference(rabbitmq)
    .WithEnvironment("AI_MODE", aiMode)
    .WithEnvironment("AI_MODEL_PATH", "/models/best.pt")
    // Verified from the checkpoint itself: yolo26m-seg segmentation head, classes {0:building,
    // 1:road}. The training notes' third "violation" class is not present in the shipped weights.
    .WithEnvironment("AI_CONFIDENCE_THRESHOLD", "0.25")
    .WithEnvironment("AI_MODEL_NAME", aiMode == "yolo" ? "yolo26m-seg-aerocompliance" : "aerocompliance-mock")
    .WithEnvironment("AI_MODEL_VERSION", aiMode == "yolo" ? "2026-08-27" : "1.0")
    .WithEnvironment("MINIO_ENDPOINT", minioS3Endpoint)
    .WithEnvironment("MINIO_ACCESS_KEY", minioRootUser)
    .WithEnvironment("MINIO_SECRET_KEY", minioRootPassword)
    .WithEnvironment("MINIO_SECURE", "false")
    .WithEnvironment("MINIO_BUCKET", "aerocompliance-images")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/ready")
    .WaitFor(rabbitmq)
    .WaitFor(minio);

// Read-only mount of the real weights. Absent on a machine without the model, in which case the
// worker is left in mock mode rather than starting a YOLO container that cannot load anything.
if (File.Exists(modelHostPath))
{
    aiWorker.WithBindMount(modelHostPath, "/models/best.pt", isReadOnly: true);
}
else if (aiMode == "yolo")
{
    throw new InvalidOperationException(
        $"AI_MODE=yolo was selected but no model file exists at {modelHostPath}. " +
        "Place the .pt weights there, or set AEROCOMPLIANCE_AI_MODE=mock.");
}

builder.AddContainer("frontend", "aerocompliance-frontend")
    .WithDockerfile("../AeroCompliance.Frontend")
    .WithReference(apiService)
    .WithEnvironment("API_UPSTREAM", apiService.GetEndpoint("https"))
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/health")
    .WaitFor(apiService);

builder.Build().Run();
