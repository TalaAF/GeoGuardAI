using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AeroCompliance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4ImageProcessingPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "compliance_evaluation_id",
                table: "violations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "longitude",
                table: "images",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<double>(
                name: "latitude",
                table: "images",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<string>(
                name: "affine_transform",
                table: "images",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "images",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "bucket",
                table: "images",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "byte_length",
                table: "images",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "content_type",
                table: "images",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "last_error_code",
                table: "images",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error_message",
                table: "images",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "object_key",
                table: "images",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "original_file_name",
                table: "images",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "processing_completed_at",
                table: "images",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "processing_request_id",
                table: "images",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "processing_started_at",
                table: "images",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "processing_status",
                table: "images",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "queued_at",
                table: "images",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "raster_height",
                table: "images",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "raster_width",
                table: "images",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_raster_crs",
                table: "images",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "upload_completed_at",
                table: "images",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE images
                SET bucket = 'legacy',
                    object_key = storage_path,
                    original_file_name = COALESCE(NULLIF(regexp_replace(storage_path, '^.*/', ''), ''), 'legacy-image'),
                    content_type = 'application/octet-stream',
                    processing_status = 'Stored',
                    upload_completed_at = created_at;
                """);

            migrationBuilder.AddColumn<int>(
                name: "class_id",
                table: "detections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "external_detection_id",
                table: "detections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "image_analysis_id",
                table: "detections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "image_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    confidence_threshold = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    source_crs = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    output_crs = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    failure_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_image_analyses", x => x.id);
                    table.ForeignKey(
                        name: "FK_image_analyses_images_image_id",
                        column: x => x.image_id,
                        principalTable: "images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    routing_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "compliance_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    detection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zoning_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    evaluation_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    measured_value = table.Column<decimal>(type: "numeric(14,3)", nullable: true),
                    required_value = table.Column<decimal>(type: "numeric(14,3)", nullable: true),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    metric_crs = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_evaluations", x => x.id);
                    table.ForeignKey(
                        name: "FK_compliance_evaluations_detections_detection_id",
                        column: x => x.detection_id,
                        principalTable: "detections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compliance_evaluations_image_analyses_image_analysis_id",
                        column: x => x.image_analysis_id,
                        principalTable: "image_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compliance_evaluations_zoning_rules_zoning_rule_id",
                        column: x => x.zoning_rule_id,
                        principalTable: "zoning_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_violations_compliance_evaluation_id",
                table: "violations",
                column: "compliance_evaluation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_images_processing_request_id",
                table: "images",
                column: "processing_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detections_image_analysis_id_external_detection_id",
                table: "detections",
                columns: new[] { "image_analysis_id", "external_detection_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compliance_evaluations_detection_id_evaluation_type",
                table: "compliance_evaluations",
                columns: new[] { "detection_id", "evaluation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compliance_evaluations_image_analysis_id",
                table: "compliance_evaluations",
                column: "image_analysis_id");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_evaluations_zoning_rule_id",
                table: "compliance_evaluations",
                column: "zoning_rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_image_analyses_image_id",
                table: "image_analyses",
                column: "image_id");

            migrationBuilder.CreateIndex(
                name: "IX_image_analyses_request_id",
                table: "image_analyses",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_event_id_consumer",
                table: "inbox_messages",
                columns: new[] { "event_id", "consumer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_published_at",
                table: "outbox_messages",
                column: "published_at");

            migrationBuilder.AddForeignKey(
                name: "FK_detections_image_analyses_image_analysis_id",
                table: "detections",
                column: "image_analysis_id",
                principalTable: "image_analyses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_violations_compliance_evaluations_compliance_evaluation_id",
                table: "violations",
                column: "compliance_evaluation_id",
                principalTable: "compliance_evaluations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_detections_image_analyses_image_analysis_id",
                table: "detections");

            migrationBuilder.DropForeignKey(
                name: "FK_violations_compliance_evaluations_compliance_evaluation_id",
                table: "violations");

            migrationBuilder.DropTable(
                name: "compliance_evaluations");

            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "image_analyses");

            migrationBuilder.DropIndex(
                name: "IX_violations_compliance_evaluation_id",
                table: "violations");

            migrationBuilder.DropIndex(
                name: "IX_images_processing_request_id",
                table: "images");

            migrationBuilder.DropIndex(
                name: "IX_detections_image_analysis_id_external_detection_id",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "compliance_evaluation_id",
                table: "violations");

            migrationBuilder.DropColumn(
                name: "affine_transform",
                table: "images");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "images");

            migrationBuilder.DropColumn(
                name: "bucket",
                table: "images");

            migrationBuilder.DropColumn(
                name: "byte_length",
                table: "images");

            migrationBuilder.DropColumn(
                name: "content_type",
                table: "images");

            migrationBuilder.DropColumn(
                name: "last_error_code",
                table: "images");

            migrationBuilder.DropColumn(
                name: "last_error_message",
                table: "images");

            migrationBuilder.DropColumn(
                name: "object_key",
                table: "images");

            migrationBuilder.DropColumn(
                name: "original_file_name",
                table: "images");

            migrationBuilder.DropColumn(
                name: "processing_completed_at",
                table: "images");

            migrationBuilder.DropColumn(
                name: "processing_request_id",
                table: "images");

            migrationBuilder.DropColumn(
                name: "processing_started_at",
                table: "images");

            migrationBuilder.DropColumn(
                name: "processing_status",
                table: "images");

            migrationBuilder.DropColumn(
                name: "queued_at",
                table: "images");

            migrationBuilder.DropColumn(
                name: "raster_height",
                table: "images");

            migrationBuilder.DropColumn(
                name: "raster_width",
                table: "images");

            migrationBuilder.DropColumn(
                name: "source_raster_crs",
                table: "images");

            migrationBuilder.DropColumn(
                name: "upload_completed_at",
                table: "images");

            migrationBuilder.DropColumn(
                name: "class_id",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "external_detection_id",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "image_analysis_id",
                table: "detections");

            migrationBuilder.AlterColumn<double>(
                name: "longitude",
                table: "images",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "latitude",
                table: "images",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);
        }
    }
}
