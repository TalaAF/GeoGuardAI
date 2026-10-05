# Phase 1 — Architecture and Contract Proposal

Status: **Approved on 2026-09-02; Phase 2 identity foundation implemented**

This document records the agreed service boundaries and proposes the contracts needed for the
mock-mode end-to-end MVP. It deliberately does not change entities, migrations, controllers, or
runtime services.

## 1. Architectural principles

1. `AeroCompliance.ApiService` is the only public business API used by the frontend.
2. PostgreSQL/PostGIS is the authoritative store for tenants, GIS data, flights, images,
   detections, compliance results, violations, and audit history.
3. MinIO stores image bytes and generated files. RabbitMQ messages carry identifiers and object
   keys, never image bytes.
4. `AeroCompliance.AiWorker` detects objects only. It does not decide whether an object is a legal
   violation and does not access the application database.
5. `AeroCompliance.ProcessingService` receives AI results, persists detections, performs PostGIS
   compliance evaluation, and creates candidate violations.
6. An AI class named `violation` remains a model indication. A violation is confirmed only after
   .NET compliance evaluation and an authorized human decision.
7. Integration messages are delivered at least once. Every publisher and consumer must therefore
   be idempotent.
8. Canonical event, database, GeoJSON, and web-map geometries use `EPSG:4326`. Metric operations for
   the current Ramallah MVP explicitly transform geometries to `EPSG:32636`.

## 2. Service ownership

| Component | Owns | Must not own |
|---|---|---|
| `AeroCompliance.ApiService` | Public REST, JWT/RBAC, tenant scope, validation, query APIs, upload orchestration | AI inference, long-running RabbitMQ consumers |
| `AeroCompliance.IngestionService` | Internal ingestion/storage workflow if retained, MinIO transfer, hashing, durable publication through an outbox | Public unauthenticated metadata acceptance, legal decisions |
| `AeroCompliance.AiWorker` | Request consumption, MinIO download, mock/YOLO inference, georeferencing, result/failure publication | PostgreSQL schema, public business API, compliance decisions |
| `AeroCompliance.ProcessingService` | Result consumption, inbox, detection persistence, spatial compliance evaluation, candidate violations, processing status | Public frontend API, model execution |
| PostgreSQL/PostGIS | Authoritative records and metric/spatial evaluation | Raw image object storage |
| MinIO | Image and generated-file objects | Business metadata and workflow state |
| RabbitMQ | Asynchronous delivery | Long-term source of truth or image payloads |

## 3. End-to-end flow

1. An authenticated user creates a flight through `ApiService`.
2. The user uploads a supported image for that flight.
3. The API validates tenant ownership, media type, size, and flight state.
4. Ingestion streams the object to MinIO while calculating SHA-256. It never trusts a client-
   supplied storage key or hash.
5. PostgreSQL records the image and an outbox `ImageProcessingRequested` message atomically after
   successful object storage.
6. An outbox dispatcher publishes the request with RabbitMQ publisher confirms and marks the
   outbox row dispatched.
7. `AeroCompliance.AiWorker` downloads the object. In mock mode it selects a deterministic scenario;
   in YOLO mode it invokes `YoloModelRunner`.
8. For a georeferenced raster, the worker reads its declared CRS and affine transform, converts mask
   pixels into raster coordinates, repairs or rejects invalid polygon geometry, transforms it to
   `EPSG:4326`, and publishes a completion event. Missing or contradictory georeferencing fails
   explicitly; geographic coordinates are never invented.
9. The worker publishes `ImageAnalysisCompleted` or `ImageAnalysisFailed` and acknowledges the
   request only after broker confirmation.
10. `AeroCompliance.ProcessingService` uses an inbox transaction to ignore duplicate result events,
    persists analysis and detections, transforms geometry to `EPSG:32636` for metre-based PostGIS
    operations, evaluates applicable validated rules, and creates candidate violations once.
11. The frontend polls or reads processing status and candidate violations through `ApiService`.
12. An Inspector, Reviewer, or Admin confirms or dismisses a candidate. The API records status
    history and an audit entry in the same transaction.

## 4. Proposed REST contract

All routes are under `/api`, use JSON except file upload/download, return DTOs rather than EF
entities, and use RFC 9457 `ProblemDetails` for errors. Collection endpoints use `page` (default 1)
and `pageSize` (default 25, maximum 100) and return:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 25,
  "totalCount": 0,
  "totalPages": 0
}
```

GeoJSON input and output uses explicit `EPSG:4326`; coordinates are longitude then latitude.

| Method and path | Authorization | Request | Success response | Notes |
|---|---|---|---|---|
| `POST /api/auth/login` | Anonymous | `{ email, password }` | `200 TokenResponse` | Preserve current route; add validation/rate limiting later |
| `POST /api/auth/refresh` | Anonymous | `{ refreshToken }` | `200 TokenResponse` | Rotation; detect reuse of a revoked token family |
| `POST /api/auth/logout` | Anonymous or authenticated | `{ refreshToken }` | `204` | Idempotent revocation |
| `GET /api/users/me` | Authenticated | — | `200 CurrentUserDto` | Includes role, permissions, municipality ID/name |
| `GET /api/municipalities` | Admin | Pagination/search/active filters | `200 Paged<MunicipalitySummary>` | Platform scope only |
| `GET /api/municipalities/{id}` | Admin or same municipality | — | `200 MunicipalityDetails` | `404` when not visible to caller |
| `POST /api/municipalities` | Admin | `CreateMunicipalityRequest` | `201 MunicipalityDetails` | Validated name/region |
| `PUT /api/municipalities/{id}` | Admin | `UpdateMunicipalityRequest` | `200 MunicipalityDetails` | Full update with concurrency token |
| `GET /api/users` | Admin/Manager | Pagination, role, active, search | `200 Paged<UserSummary>` | Manager is forced to own municipality |
| `GET /api/users/{id}` | Admin/Manager | — | `200 UserDetails` | Tenant scoped |
| `POST /api/users` | Admin/Manager | name, email, role, municipality, initial password | `201 UserDetails` | Manager cannot grant platform Admin |
| `PUT /api/users/{id}` | Admin/Manager | editable profile + concurrency token | `200 UserDetails` | Tenant and role restrictions |
| `POST /api/users/{id}/activate` | Admin/Manager | optional reason | `204` | Audited |
| `POST /api/users/{id}/deactivate` | Admin/Manager | reason | `204` | Revokes active refresh tokens |
| `GET /api/zones` | Authenticated GIS/read role | bbox, municipality, filters, pagination | `200 Paged<ZoneSummary>` | Non-admin tenant forced by claims |
| `GET /api/zones/{id}` | Authenticated GIS/read role | — | `200 ZoneDetails` | Geometry as GeoJSON |
| `POST /api/zones` | Admin/Manager/GISAnalyst | metadata + GeoJSON geometry | `201 ZoneDetails` | Validate SRID/geometry |
| `PUT /api/zones/{id}` | Admin/Manager/GISAnalyst | DTO + concurrency token | `200 ZoneDetails` | Tenant scoped |
| `GET/POST/PUT /api/blocks[...]` | Same GIS policies | DTOs equivalent to zones | Standard `200/201` | Block must belong to visible zone |
| `GET/POST/PUT /api/parcels[...]` | Same GIS policies | DTOs + bbox/block filters | Standard `200/201` | Geometry validation and spatial index |
| `GET/POST/PUT /api/roads[...]` | Same GIS policies | DTOs + bbox/type filters | Standard `200/201` | Right-of-way polygons in 4326 |
| `GET/POST/PUT /api/zoning-rules[...]` | Read authenticated; write GISAnalyst/Admin | rule target/value/unit/source | Standard `200/201` | Only validated active rules are evaluated |
| `POST /api/zoning-rules/{id}/validate` | GISAnalyst/Admin | `{ concurrencyToken }` | `200 ZoningRuleDetails` | Records validator/time/audit |
| `GET /api/flights` | Authenticated | status/operator/date/pagination | `200 Paged<FlightSummary>` | Tenant scoped; operator may be limited to own flights |
| `GET /api/flights/{id}` | Authenticated | — | `200 FlightDetails` | Includes missions/media counts |
| `POST /api/flights` | DroneOperator/Admin/Manager | mission name, schedule, planned area | `201 FlightDetails` | Municipality inferred unless Admin |
| `PUT /api/flights/{id}` | Authorized owner/manager | DTO + concurrency token | `200 FlightDetails` | Does not bypass transition rules |
| `POST /api/flights/{id}/status-transitions` | Authorized owner/manager | `{ targetStatus, reason, concurrencyToken }` | `200 FlightDetails` | Enforces allowed state machine |
| `GET /api/flights/{id}/missions` | Authenticated | pagination | `200 Paged<MissionDto>` | Tenant scoped |
| `POST /api/flights/{id}/missions` | DroneOperator/Admin/Manager | purpose, altitude, overlap, notes | `201 MissionDto` | Flight must be editable |
| `POST /api/flights/{id}/images` | DroneOperator/Admin/Manager | `multipart/form-data`: file + capture metadata | `202 ImageReceiptDto` | Stores MinIO object, DB row, then outbox |
| `GET /api/flights/{id}/images` | Authenticated | pagination/status | `200 Paged<ImageSummary>` | Tenant scoped |
| `GET /api/images/{id}/processing-status` | Authenticated | — | `200 ImageProcessingStatusDto` | Status, attempts, timestamps, safe failure code |
| `POST /api/images/{id}/retry-processing` | Admin/Manager/DroneOperator | optional reason | `202 ImageProcessingStatusDto` | Only retryable terminal failures |
| `GET /api/detections` | Inspector/Reviewer/GISAnalyst/Admin | image/flight/class/bbox/pagination | `200 Paged<DetectionSummary>` | AI facts, not legal decisions |
| `GET /api/detections/{id}` | Same | — | `200 DetectionDetails` | GeoJSON and model metadata |
| `GET /api/violations` | Authenticated review/read roles | status/risk/type/flight/bbox/date/pagination | `200 Paged<ViolationSummary>` | Tenant scoped candidates and reviewed records |
| `GET /api/violations/{id}` | Authenticated review/read roles | — | `200 ViolationDetails` | Includes detection, evidence, rule and history |
| `POST /api/violations/{id}/confirm` | Inspector/Reviewer/Admin | notes + concurrency token | `200 ViolationDetails` | Same transaction: state/history/audit |
| `POST /api/violations/{id}/dismiss` | Inspector/Reviewer/Admin | required reason + concurrency token | `200 ViolationDetails` | Same transaction: state/history/audit |
| `GET /api/dashboard/summary` | Authenticated | optional date range | `200 DashboardSummaryDto` | Tenant scope applied server-side |
| `GET /api/audit` | Admin/Manager; narrower self/entity views later | actor/action/entity/date/pagination | `200 Paged<AuditEntryDto>` | Immutable records |
| `GET /api/reports` | Authorized municipality roles | filters/pagination | `200 Paged<ReportSummary>` | Metadata only |
| `POST /api/violations/{id}/reports` | Reviewer/Admin | report options | `202 ReportReceiptDto` | Generation may be asynchronous |
| `GET /api/reports/{id}/download` | Authorized municipality roles | — | redirect or streamed PDF | Never exposes raw MinIO credentials |

Expected common failures are `400` invalid syntax, `401` missing/invalid identity, `403` forbidden,
`404` missing or cross-tenant-hidden resource, `409` illegal state/duplicate/concurrency conflict,
`413` oversized upload, `415` unsupported media, and `422` semantically invalid geometry.

## 5. Proposed RabbitMQ contracts

All events use camelCase JSON, ISO-8601 UTC timestamps, a positive integer `schemaVersion`, and a
RabbitMQ `messageId` equal to `eventId`. Events contain no secrets, presigned URLs, or image bytes.

### Common envelope fields

```json
{
  "eventId": "uuid",
  "schemaVersion": 1,
  "correlationId": "uuid",
  "causationId": "uuid-or-null",
  "occurredAt": "2026-01-01T00:00:00Z"
}
```

| Event | Routing key | Producer | Consumer | Event-specific payload |
|---|---|---|---|---|
| `ImageProcessingRequested` | `image.processing.requested.v1` | .NET outbox | AI Worker | `requestId`, `imageId`, `flightId`, `municipalityId`, `bucket`, `objectKey`, `sha256`, `contentType`, optional `mockScenario` in Development only |
| `ImageAnalysisStarted` | `image.analysis.started.v1` | AI Worker | Processing Service/observability | `requestEventId`, IDs, `attempt`, `workerInstance`, `mode`, `modelName`, `modelVersion`, `startedAt` |
| `ImageAnalysisCompleted` | `image.analysis.completed.v1` | AI Worker | Processing Service | request IDs, model metadata, threshold, `geometryCrs: EPSG:4326`, raster source CRS, detections, `completedAt`, duration |
| `ImageAnalysisFailed` | `image.analysis.failed.v1` | AI Worker | Processing Service | request IDs, attempt, stage, stable error code, sanitized message, `retryable`, `failedAt` |

Each completed detection contains:

```json
{
  "detectionId": "deterministic-uuid",
  "classId": 0,
  "className": "building",
  "confidence": 0.93,
  "geometry": {
    "type": "MultiPolygon",
    "coordinates": []
  }
}
```

`detectionId` is deterministically derived from the processing request and stable detection ordinal
or mask identity. Reprocessing under a new request creates a new analysis while redelivery of the
same request produces the same IDs.

### Compatibility with `ImageUploaded`

The existing `ImageUploaded` record and `image.uploaded` route remain unchanged during transition.
New production flows publish only `ImageProcessingRequested` after object storage and DB persistence.
A temporary compatibility adapter may accept legacy `ImageUploaded`, validate its referenced image
against PostgreSQL/MinIO, and create one requested event using the legacy message ID as its
idempotency source. Dual-publishing both events for one upload is prohibited because it creates two
processing requests.

### Topology and reliability

- Topic exchange: `aerocompliance.processing`.
- AI request queue: `aerocompliance.ai.image-processing.v1`.
- .NET result queue: `aerocompliance.processing.image-analysis-results.v1`.
- Durable exchanges/queues and persistent messages.
- Manual consumer acknowledgement only after durable side effects or confirmed result publication.
- Publisher confirms for all integration events.
- Retry queues use bounded delay and attempt headers; exhausted/non-retryable events go to a DLQ.
- Logs include event, correlation, image, flight, and municipality IDs but never credentials.

## 6. Processing state lifecycle

One user-visible state should summarize the full pipeline while detailed attempts remain in related
records.

```text
Uploading
   ├── failure → UploadFailed
   ▼
Stored
   ▼
Queued
   ▼
Analyzing
   ├── retryable failure → Queued (new attempt, bounded)
   └── terminal failure → AnalysisFailed
   ▼
AnalysisCompleted
   ▼
EvaluatingCompliance
   ├── retryable failure → EvaluatingCompliance
   └── terminal failure → ComplianceFailed
   ▼
Completed
```

`Completed` can contain zero detections and zero candidate violations; that is a successful result.
Violation review status is a separate lifecycle and must not be mixed with image processing status.

Allowed retry creates a new `requestId`/analysis attempt, retains the same `imageId`, and records
causation to the prior failed request. A duplicate delivery retains the same event and request IDs
and must not create a new attempt.

## 7. Idempotency ownership

| Boundary | Idempotency key | Mechanism |
|---|---|---|
| Upload HTTP request | Optional client `Idempotency-Key` scoped to user/flight | Unique request record returns the original receipt on retry |
| DB → RabbitMQ request | `OutboxMessage.EventId` | Image status and outbox row written in one DB transaction |
| AI request handling | `requestId` plus deterministic output `eventId` | Reprocessing a redelivery emits byte-equivalent logical results; confirm publish before ack |
| .NET result consumer | `InboxMessage.EventId` | Inbox insert, analysis/detections/status, violations and outbox changes in one transaction |
| Detection insertion | `(AnalysisId, ExternalDetectionId)` | Unique database constraint |
| Compliance evaluation | `(AnalysisId, DetectionId, RuleId, RuleVersion)` | Unique evaluation record |
| Candidate violation | `ComplianceEvaluationId` | One-to-one/unique FK prevents duplicates |
| Human decision | violation ID + concurrency token | Optimistic concurrency and transactional history/audit |

The Python worker does not need application-database access. Deterministic IDs make duplicate result
publication harmless, while the .NET inbox remains the authoritative duplicate barrier.

## 8. Proposed database changes — no migration yet

### Images and processing

- Replace ambiguous `StoragePath` usage with explicit `Bucket` and `ObjectKey`, or define
  `StoragePath` strictly as an object key and add `Bucket`.
- Add original file name, MIME type, byte length, object ETag/version when available, and upload
  completion timestamp.
- Add `ProcessingStatus`, queued/started/completed timestamps, attempt count, last safe error code,
  and optimistic concurrency token.
- Add raster metadata when known: source CRS, affine transform, pixel width/height. Metadata must be
  observed from the file, not invented from capture GPS.

### Analysis and detections

- Add `ImageAnalysis` to represent one request/run: request/event IDs, image, mode, model name/version,
  threshold, source/output CRS, status, timing, attempt, and failure details.
- Link `Detection` to `ImageAnalysis` and add external deterministic detection ID plus numeric class ID.
- Add unique `(image_analysis_id, external_detection_id)`.
- Keep canonical `geometry(MultiPolygon, 4326)` and GiST index.
- Correct the current source check if the intended rule is exactly one of image/orthomosaic rather
  than the current inclusive `OR`.

### Messaging reliability

- Add `OutboxMessage` with unique event ID, type, schema version, payload JSONB, occurrence time,
  dispatched time, and attempt/error metadata.
- Add `InboxMessage` with unique event ID, consumer name, received/completed time, and payload hash.
- Add `ProcessingAttempt` if operational history needs more detail than `ImageAnalysis` supplies.

### Compliance and violations

- Add `ComplianceEvaluation` with analysis/detection/rule identity, rule version snapshot, measured
  and required values, metric CRS, outcome, and timestamps.
- Make candidate violation creation unique by compliance evaluation.
- Preserve separate `Detection` and `Violation` entities.
- Add optimistic concurrency to `Violation` and ensure every transition appends
  `ViolationStatusHistory` and `AuditLog` atomically.
- Consider renaming or aliasing the status mismatch `confirmed_under_notification` versus the
  frontend's `confirmed_notification`; one canonical code must be approved before migration.

### Tenant safety

- Include `municipalityId` in JWT claims and server-side tenant context.
- Enforce tenant filtering in application queries and explicit ownership validation on writes.
- Consider denormalized municipality IDs only where they improve enforceable constraints/indexing;
  do not rely solely on client parameters.

## 9. Proposed Python AI Worker structure

```text
src/AeroCompliance.AiWorker/
├── Dockerfile
├── pyproject.toml
├── README.md
├── .env.example
├── models/
│   └── README.md
├── src/aerocompliance_ai/
│   ├── __init__.py
│   ├── main.py
│   ├── config.py
│   ├── health.py
│   ├── contracts.py
│   ├── messaging/
│   │   ├── consumer.py
│   │   ├── publisher.py
│   │   └── topology.py
│   ├── storage/
│   │   └── minio_client.py
│   ├── models/
│   │   ├── base.py
│   │   ├── mock.py
│   │   └── yolo.py
│   ├── geospatial/
│   │   ├── raster_metadata.py
│   │   ├── mask_transform.py
│   │   └── geometry_validation.py
│   └── processing/
│       └── handler.py
└── tests/
    ├── unit/
    ├── contracts/
    └── geospatial/
```

`ModelRunner.analyze(image_path, context)` returns model-space masks/classes/confidence. The handler
owns georeferencing and event shaping. `MockModelRunner` supports deterministic no-detection,
building-only, parcel candidate, road candidate, failure, and duplicate-publication scenarios.
`YoloModelRunner` imports heavy YOLO dependencies lazily and fails readiness in `AI_MODE=yolo` when
the configured model is missing or invalid. Mock mode is the default and needs no `.pt` file.

The health server exposes operational `GET /health` (process alive) and `GET /ready` (RabbitMQ,
MinIO, configuration, and selected runner ready). It exposes no business endpoints.

## 10. Proposed .NET Processing Service structure

```text
src/AeroCompliance.ProcessingService/
├── AeroCompliance.ProcessingService.csproj
├── Program.cs
├── appsettings.json
├── Consumers/
│   └── ImageAnalysisResultConsumer.cs
├── Messaging/
│   ├── ProcessingTopologyInitializer.cs
│   └── RetryPolicy.cs
├── Processing/
│   ├── AnalysisResultHandler.cs
│   ├── DetectionMapper.cs
│   └── ProcessingStatusService.cs
├── Compliance/
│   ├── IComplianceEvaluator.cs
│   ├── PostGisComplianceEvaluator.cs
│   └── CandidateViolationFactory.cs
└── Idempotency/
    └── InboxService.cs
```

It references `Domain`, `Infrastructure`, `Contracts`, and `ServiceDefaults`; it has no controllers.
The consumer uses a scoped handler and one database transaction per result event. PostGIS performs
metric operations only after explicit `ST_Transform(..., 32636)`.

## 11. Future Aspire orchestration

- Add `AeroCompliance.ProcessingService` as a project reference with PostgreSQL and RabbitMQ
  references and dependency health waits.
- Add `AeroCompliance.AiWorker` as a Dockerfile-built container with RabbitMQ/MinIO configuration,
  `AI_MODE=mock` by default, model path configuration, `/health` and `/ready` health checks, and a
  model volume only when YOLO mode is requested.
- Do not pass database credentials to the AI Worker.
- Do not make AI readiness depend on a real model in mock mode.
- Add the frontend later as a separately containerized Aspire resource after REST contracts settle.

## 12. Approved decisions

1. Authenticated upload orchestration will live directly in `ApiService`, backed by reusable
   infrastructure services. There will be no second public ingestion path.
2. The canonical confirmed status is `confirmed_under_notification`. The frontend naming mismatch
   is deferred to the frontend phase.
3. The configurable upload limit is 250 MB. GeoTIFF is required for real georeferenced processing;
   JPEG and PNG are accepted only for deterministic Development mock scenarios unless a separately
   validated georeferencing sidecar is introduced later.
4. `Parcel.OwnerName` is sensitive data. It remains stored, but may be returned only to authorized
   Admin, Manager, Inspector, or Reviewer workflows; general GIS/list responses must redact it.
5. Canonical geometry storage and interchange use `EPSG:4326`; Ramallah metric operations use
   `EPSG:32636`. Imports must honor declared source CRS and reject missing or contradictory metadata.

## 13. Phase 2 identity implementation

- Access tokens use standard `sub`, email, and role claims. Municipality-bound users also receive
  the optional `municipality_id` claim; platform administrators do not receive a fabricated tenant.
- Request handlers obtain identity through scoped `ICurrentUserContext`, not by repeatedly parsing
  `HttpContext.User`. Invalid or missing GUID claims are treated as unavailable and never throw.
- `GET /api/users/me` returns a safe DTO containing identity, role, optional municipality, and active
  status. It never serializes password hashes, refresh tokens, audit collections, or EF entities.
- Full server-side tenant query enforcement remains a later phase. Adding the claim and context does
  not by itself authorize cross-tenant access; each future query and write must apply ownership rules.
- Refresh-token rotation and revocation are covered in Phase 2. Token-family reuse detection remains
  deferred because it requires an explicit persistence-model and migration change.
