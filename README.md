# AeroCompliance

Drone-based municipal compliance & monitoring platform. **Epic 1 — Infrastructure & Environment
Setup** (scaffolding only; no business logic yet).

## Stack

- .NET 10, ASP.NET Core Web API, orchestrated with **.NET Aspire**
- Microservices with async messaging over **RabbitMQ**
- **PostgreSQL + PostGIS** for geospatial data (via EF Core + NetTopologySuite)
- **MinIO** (S3-compatible) for object storage
- JWT + refresh tokens, policy-based **RBAC**

## Projects (`src/`)

| Project | Purpose |
|---|---|
| `AeroCompliance.AppHost` | Aspire orchestrator (Postgres/PostGIS + pgAdmin, RabbitMQ, MinIO) |
| `AeroCompliance.ServiceDefaults` | Shared telemetry / health / resilience |
| `AeroCompliance.ApiService` | Main Web API: auth, users, municipalities, violations |
| `AeroCompliance.IngestionService` | Drone image ingestion → publishes `ImageUploaded` to RabbitMQ |
| `AeroCompliance.Domain` | Entities + enums |
| `AeroCompliance.Infrastructure` | EF Core `AeroComplianceDbContext`, PostGIS config, migrations |
| `AeroCompliance.Contracts` | Shared DTOs + RabbitMQ message contracts |

## Prerequisites

- .NET 10 SDK
- A container runtime (Docker Desktop / Podman) — Aspire starts Postgres, RabbitMQ and MinIO as containers.

## Run

```bash
dotnet run --project src/AeroCompliance.AppHost
```

The Aspire dashboard lists all resources and endpoints (API + Swagger, Ingestion, pgAdmin,
RabbitMQ management UI, MinIO console). Dev credentials for the containers live under
`Parameters` in `src/AeroCompliance.AppHost/appsettings.json` — **replace these with user-secrets
for anything beyond local dev.**

## Database migrations

The initial migration (`InitialCreate`) is already generated under
`src/AeroCompliance.Infrastructure/Persistence/Migrations`. The `dotnet-ef` CLI is pinned as a
local tool (`dotnet-tools.json`).

```bash
# add a migration
dotnet dotnet-ef migrations add <Name> \
  --project src/AeroCompliance.Infrastructure \
  --startup-project src/AeroCompliance.Infrastructure

# apply against a running Postgres (or set AEROCOMPLIANCE_DESIGN_CONNECTION)
dotnet dotnet-ef database update \
  --project src/AeroCompliance.Infrastructure \
  --startup-project src/AeroCompliance.Infrastructure
```

> The `images.geom` column is a PostGIS `GENERATED ALWAYS AS ... STORED` column, mapped in EF as a
> stored computed column (`HasComputedColumnSql(..., stored: true)`); the database owns its value.
