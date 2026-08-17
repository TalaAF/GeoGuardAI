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
    .WithDataVolume("aerocompliance-pgdata")
    .WithPgAdmin(); // local inspection UI

var aeroDb = postgres.AddDatabase("aerocompliancedb");

// ---------------------------------------------------------------------------
// RabbitMQ (with the management plugin / UI)
// ---------------------------------------------------------------------------
var rabbitUser = builder.AddParameter("rabbitmq-user", secret: true);
var rabbitPassword = builder.AddParameter("rabbitmq-password", secret: true);

var rabbitmq = builder.AddRabbitMQ("messaging", rabbitUser, rabbitPassword)
    .WithDataVolume("aerocompliance-rabbitdata")
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
    .WaitFor(minio);

builder.AddProject<Projects.AeroCompliance_IngestionService>("ingestionservice")
    .WithReference(aeroDb).WaitFor(aeroDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(apiService) // may call the API for validation/lookups later
    .WithEnvironment("MinIO__Endpoint", minioS3Endpoint)
    .WithEnvironment("MinIO__AccessKey", minioRootUser)
    .WithEnvironment("MinIO__SecretKey", minioRootPassword)
    .WaitFor(minio);

builder.Build().Run();
