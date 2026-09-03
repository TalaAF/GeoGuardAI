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

builder.AddContainer("aiworker", "aerocompliance-ai-worker")
    .WithDockerfile("../AeroCompliance.AiWorker")
    // "geospatial" profile (rasterio/shapely/pyproj, no ultralytics/PyTorch) so a normal local run
    // can read GeoTIFF georeferencing without pulling in the much larger YOLO/PyTorch stack. This is
    // independent of AI_MODE below, which is what actually selects mock vs. real inference — switch
    // this build arg to "requirements.txt" for a smaller/faster core-only image (no GeoTIFF metadata
    // extraction), or "requirements-yolo.txt" once a real model is being tested (see the AI Worker
    // README's "Dependency profiles" section).
    .WithBuildArg("AI_REQUIREMENTS", "requirements-geospatial.txt")
    .WithReference(rabbitmq)
    .WithEnvironment("AI_MODE", "mock")
    .WithEnvironment("AI_MODEL_PATH", "/models/model.pt")
    .WithEnvironment("AI_CONFIDENCE_THRESHOLD", "0.50")
    .WithEnvironment("AI_MODEL_NAME", "aerocompliance-mock")
    .WithEnvironment("AI_MODEL_VERSION", "1.0")
    .WithEnvironment("MINIO_ENDPOINT", minioS3Endpoint)
    .WithEnvironment("MINIO_ACCESS_KEY", minioRootUser)
    .WithEnvironment("MINIO_SECRET_KEY", minioRootPassword)
    .WithEnvironment("MINIO_SECURE", "false")
    .WithEnvironment("MINIO_BUCKET", "aerocompliance-images")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/ready")
    .WaitFor(rabbitmq)
    .WaitFor(minio);

builder.AddContainer("frontend", "aerocompliance-frontend")
    .WithDockerfile("../AeroCompliance.Frontend")
    .WithReference(apiService)
    .WithEnvironment("API_UPSTREAM", apiService.GetEndpoint("https"))
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/health")
    .WaitFor(apiService);

builder.Build().Run();
