using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AeroCompliance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "municipalities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mou_signed_at = table.Column<DateOnly>(type: "date", nullable: true),
                    api_access_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_pilot = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_municipalities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "violation_statuses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    label_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_violation_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    municipality_id = table.Column<Guid>(type: "uuid", nullable: false),
                    road_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    road_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    width_m = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    speed_limit_kmh = table.Column<int>(type: "integer", nullable: true),
                    is_sensitive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    geom = table.Column<MultiPolygon>(type: "geometry(MultiPolygon, 4326)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roads", x => x.id);
                    table.ForeignKey(
                        name: "FK_roads_municipalities_municipality_id",
                        column: x => x.municipality_id,
                        principalTable: "municipalities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    municipality_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    zone_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    geom = table.Column<MultiPolygon>(type: "geometry(MultiPolygon, 4326)", nullable: false),
                    source_document = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zones", x => x.id);
                    table.ForeignKey(
                        name: "FK_zones_municipalities_municipality_id",
                        column: x => x.municipality_id,
                        principalTable: "municipalities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    permission_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    municipality_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_municipalities_municipality_id",
                        column: x => x.municipality_id,
                        principalTable: "municipalities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_users_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    block_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    geom = table.Column<MultiPolygon>(type: "geometry(MultiPolygon, 4326)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blocks", x => x.id);
                    table.ForeignKey(
                        name: "FK_blocks_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    before_state = table.Column<string>(type: "jsonb", nullable: true),
                    after_state = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "flights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    municipality_id = table.Column<Guid>(type: "uuid", nullable: false),
                    drone_operator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    planned_area = table.Column<MultiPolygon>(type: "geometry(MultiPolygon, 4326)", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "planned"),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    weather_conditions = table.Column<string>(type: "jsonb", nullable: true),
                    quality_flag = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flights", x => x.id);
                    table.ForeignKey(
                        name: "FK_flights_municipalities_municipality_id",
                        column: x => x.municipality_id,
                        principalTable: "municipalities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_flights_users_drone_operator_id",
                        column: x => x.drone_operator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zoning_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zone_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    rule_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rule_value = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_document = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    extracted_by_ai = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_validated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    validated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zoning_rules", x => x.id);
                    table.CheckConstraint("ck_zoning_rules_target", "zone_id IS NOT NULL OR zone_type IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_zoning_rules_users_validated_by",
                        column: x => x.validated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_zoning_rules_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parcels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    block_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parcel_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    owner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    zone_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    area_m2 = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    geom = table.Column<MultiPolygon>(type: "geometry(MultiPolygon, 4326)", nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "municipality_gis"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcels", x => x.id);
                    table.ForeignKey(
                        name: "FK_parcels_blocks_block_id",
                        column: x => x.block_id,
                        principalTable: "blocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flight_missions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    flight_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    altitude_m = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    overlap_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flight_missions", x => x.id);
                    table.ForeignKey(
                        name: "FK_flight_missions_flights_flight_id",
                        column: x => x.flight_id,
                        principalTable: "flights",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    flight_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    sha256_hash = table.Column<string>(type: "char(64)", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    altitude_m = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    camera_heading_deg = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    gps_accuracy_cm = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    geom = table.Column<Point>(type: "geometry(Point, 4326)", nullable: true, computedColumnSql: "ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)", stored: true),
                    is_part_of_ortho = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_images_flights_flight_id",
                        column: x => x.flight_id,
                        principalTable: "flights",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orthomosaics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    flight_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    bounds = table.Column<Polygon>(type: "geometry(Polygon, 4326)", nullable: false),
                    resolution_cm_per_px = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orthomosaics", x => x.id);
                    table.ForeignKey(
                        name: "FK_orthomosaics_flights_flight_id",
                        column: x => x.flight_id,
                        principalTable: "flights",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "videos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    flight_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    sha256_hash = table.Column<string>(type: "char(64)", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_videos", x => x.id);
                    table.ForeignKey(
                        name: "FK_videos_flights_flight_id",
                        column: x => x.flight_id,
                        principalTable: "flights",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "detections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    orthomosaic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    class_label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    geom = table.Column<MultiPolygon>(type: "geometry(MultiPolygon, 4326)", nullable: false),
                    area_m2 = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    perimeter_m = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    height_m = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detections", x => x.id);
                    table.CheckConstraint("ck_detections_confidence", "confidence BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_detections_source", "orthomosaic_id IS NOT NULL OR image_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_detections_images_image_id",
                        column: x => x.image_id,
                        principalTable: "images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detections_orthomosaics_orthomosaic_id",
                        column: x => x.orthomosaic_id,
                        principalTable: "orthomosaics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "police_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    detection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    road_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_police_alerts", x => x.id);
                    table.ForeignKey(
                        name: "FK_police_alerts_detections_detection_id",
                        column: x => x.detection_id,
                        principalTable: "detections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_police_alerts_roads_road_id",
                        column: x => x.road_id,
                        principalTable: "roads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "violations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    detection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parcel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    road_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    block_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    violation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    measured_value = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    required_value = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    exceedance_value = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    exceedance_area_m2 = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    ai_confidence_score = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    is_zero_state = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    current_status_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    evidence_image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    geom = table.Column<MultiPolygon>(type: "geometry(MultiPolygon, 4326)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_violations", x => x.id);
                    table.ForeignKey(
                        name: "FK_violations_blocks_block_id",
                        column: x => x.block_id,
                        principalTable: "blocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_violations_detections_detection_id",
                        column: x => x.detection_id,
                        principalTable: "detections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_violations_images_evidence_image_id",
                        column: x => x.evidence_image_id,
                        principalTable: "images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_violations_parcels_parcel_id",
                        column: x => x.parcel_id,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_violations_roads_road_id",
                        column: x => x.road_id,
                        principalTable: "roads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_violations_violation_statuses_current_status_id",
                        column: x => x.current_status_id,
                        principalTable: "violation_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_violations_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_violations_zoning_rules_rule_id",
                        column: x => x.rule_id,
                        principalTable: "zoning_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    violation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    recipient_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "pending"),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_violations_violation_id",
                        column: x => x.violation_id,
                        principalTable: "violations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    violation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    generated_by = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ai_draft"),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_reports_users_signed_off_by",
                        column: x => x.signed_off_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reports_violations_violation_id",
                        column: x => x.violation_id,
                        principalTable: "violations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "violation_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    violation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_violation_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_violation_status_history_users_changed_by",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_violation_status_history_violation_statuses_status_id",
                        column: x => x.status_id,
                        principalTable: "violation_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_violation_status_history_violations_violation_id",
                        column: x => x.violation_id,
                        principalTable: "violations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "violation_statuses",
                columns: new[] { "id", "code", "label_ar", "label_en" },
                values: new object[,]
                {
                    { 1, "under_review", "بانتظار المراجعة", "Under review" },
                    { 2, "confirmed_under_notification", "مؤكدة - جاري الإخطار", "Confirmed – under notification" },
                    { 3, "notified", "تم الإخطار", "Notified" },
                    { 4, "under_dispute", "تحت الاعتراض", "Under dispute/appeal" },
                    { 5, "paused", "متوقفة مؤقتًا", "Paused" },
                    { 6, "resolved", "تمت المعالجة", "Removed / resolved" },
                    { 7, "dismissed", "مرفوضة", "Dismissed" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_actor_id",
                table: "audit_logs",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_type_entity_id",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_blocks_geom",
                table: "blocks",
                column: "geom")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_blocks_zone_id_block_number",
                table: "blocks",
                columns: new[] { "zone_id", "block_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detections_class_label",
                table: "detections",
                column: "class_label");

            migrationBuilder.CreateIndex(
                name: "IX_detections_geom",
                table: "detections",
                column: "geom")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_detections_image_id",
                table: "detections",
                column: "image_id");

            migrationBuilder.CreateIndex(
                name: "IX_detections_orthomosaic_id",
                table: "detections",
                column: "orthomosaic_id");

            migrationBuilder.CreateIndex(
                name: "IX_flight_missions_flight_id",
                table: "flight_missions",
                column: "flight_id");

            migrationBuilder.CreateIndex(
                name: "IX_flights_drone_operator_id",
                table: "flights",
                column: "drone_operator_id");

            migrationBuilder.CreateIndex(
                name: "IX_flights_municipality_id",
                table: "flights",
                column: "municipality_id");

            migrationBuilder.CreateIndex(
                name: "IX_flights_planned_area",
                table: "flights",
                column: "planned_area")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_images_flight_id",
                table: "images",
                column: "flight_id");

            migrationBuilder.CreateIndex(
                name: "IX_images_geom",
                table: "images",
                column: "geom")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_violation_id",
                table: "notifications",
                column: "violation_id");

            migrationBuilder.CreateIndex(
                name: "IX_orthomosaics_bounds",
                table: "orthomosaics",
                column: "bounds")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_orthomosaics_flight_id",
                table: "orthomosaics",
                column: "flight_id");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_block_id_parcel_number",
                table: "parcels",
                columns: new[] { "block_id", "parcel_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parcels_geom",
                table: "parcels",
                column: "geom")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_code",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_police_alerts_detection_id",
                table: "police_alerts",
                column: "detection_id");

            migrationBuilder.CreateIndex(
                name: "IX_police_alerts_road_id",
                table: "police_alerts",
                column: "road_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reports_signed_off_by",
                table: "reports",
                column: "signed_off_by");

            migrationBuilder.CreateIndex(
                name: "IX_reports_violation_id",
                table: "reports",
                column: "violation_id");

            migrationBuilder.CreateIndex(
                name: "IX_roads_geom",
                table: "roads",
                column: "geom")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_roads_municipality_id_road_code",
                table: "roads",
                columns: new[] { "municipality_id", "road_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_municipality_id",
                table: "users",
                column: "municipality_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_videos_flight_id",
                table: "videos",
                column: "flight_id");

            migrationBuilder.CreateIndex(
                name: "IX_violation_status_history_changed_by",
                table: "violation_status_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_violation_status_history_status_id",
                table: "violation_status_history",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "IX_violation_status_history_violation_id",
                table: "violation_status_history",
                column: "violation_id");

            migrationBuilder.CreateIndex(
                name: "IX_violation_statuses_code",
                table: "violation_statuses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_violations_block_id",
                table: "violations",
                column: "block_id");

            migrationBuilder.CreateIndex(
                name: "IX_violations_current_status_id",
                table: "violations",
                column: "current_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_violations_detection_id",
                table: "violations",
                column: "detection_id");

            migrationBuilder.CreateIndex(
                name: "IX_violations_evidence_image_id",
                table: "violations",
                column: "evidence_image_id");

            migrationBuilder.CreateIndex(
                name: "IX_violations_geom",
                table: "violations",
                column: "geom")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_violations_parcel_id",
                table: "violations",
                column: "parcel_id");

            migrationBuilder.CreateIndex(
                name: "IX_violations_road_id",
                table: "violations",
                column: "road_id");

            migrationBuilder.CreateIndex(
                name: "IX_violations_rule_id",
                table: "violations",
                column: "rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_violations_zone_id",
                table: "violations",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_zones_geom",
                table: "zones",
                column: "geom")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_zones_municipality_id_zone_number",
                table: "zones",
                columns: new[] { "municipality_id", "zone_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zoning_rules_rule_key",
                table: "zoning_rules",
                column: "rule_key",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_zoning_rules_validated_by",
                table: "zoning_rules",
                column: "validated_by");

            migrationBuilder.CreateIndex(
                name: "IX_zoning_rules_zone_id",
                table: "zoning_rules",
                column: "zone_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "flight_missions");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "police_alerts");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "videos");

            migrationBuilder.DropTable(
                name: "violation_status_history");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "violations");

            migrationBuilder.DropTable(
                name: "detections");

            migrationBuilder.DropTable(
                name: "parcels");

            migrationBuilder.DropTable(
                name: "roads");

            migrationBuilder.DropTable(
                name: "violation_statuses");

            migrationBuilder.DropTable(
                name: "zoning_rules");

            migrationBuilder.DropTable(
                name: "images");

            migrationBuilder.DropTable(
                name: "orthomosaics");

            migrationBuilder.DropTable(
                name: "blocks");

            migrationBuilder.DropTable(
                name: "flights");

            migrationBuilder.DropTable(
                name: "zones");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "municipalities");

            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}
