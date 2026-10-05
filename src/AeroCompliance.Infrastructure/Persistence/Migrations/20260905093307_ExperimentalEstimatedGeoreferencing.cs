using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroCompliance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExperimentalEstimatedGeoreferencing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "georeferencing_confidence",
                table: "violations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_approximate",
                table: "violations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_experimental",
                table: "violations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "requires_manual_review",
                table: "violations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "result_source",
                table: "violations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_georeferencing_method",
                table: "violations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "georeferencing_confidence",
                table: "image_analyses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "georeferencing_method",
                table: "image_analyses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "georeferencing_status",
                table: "image_analyses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "model_only_detection_count",
                table: "image_analyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "model_only_detections",
                table: "image_analyses",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "trusted_roi_radius_m",
                table: "image_analyses",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "distance_from_anchor_m",
                table: "detections",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "georeferencing_confidence",
                table: "detections",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_approximate",
                table: "detections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "requires_manual_review",
                table: "detections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "result_source",
                table: "detections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_georeferencing_method",
                table: "detections",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "georeferencing_confidence",
                table: "compliance_evaluations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_approximate",
                table: "compliance_evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "requires_manual_review",
                table: "compliance_evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "result_source",
                table: "compliance_evaluations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_georeferencing_method",
                table: "compliance_evaluations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "georeferencing_confidence",
                table: "violations");

            migrationBuilder.DropColumn(
                name: "is_approximate",
                table: "violations");

            migrationBuilder.DropColumn(
                name: "is_experimental",
                table: "violations");

            migrationBuilder.DropColumn(
                name: "requires_manual_review",
                table: "violations");

            migrationBuilder.DropColumn(
                name: "result_source",
                table: "violations");

            migrationBuilder.DropColumn(
                name: "source_georeferencing_method",
                table: "violations");

            migrationBuilder.DropColumn(
                name: "georeferencing_confidence",
                table: "image_analyses");

            migrationBuilder.DropColumn(
                name: "georeferencing_method",
                table: "image_analyses");

            migrationBuilder.DropColumn(
                name: "georeferencing_status",
                table: "image_analyses");

            migrationBuilder.DropColumn(
                name: "model_only_detection_count",
                table: "image_analyses");

            migrationBuilder.DropColumn(
                name: "model_only_detections",
                table: "image_analyses");

            migrationBuilder.DropColumn(
                name: "trusted_roi_radius_m",
                table: "image_analyses");

            migrationBuilder.DropColumn(
                name: "distance_from_anchor_m",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "georeferencing_confidence",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "is_approximate",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "requires_manual_review",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "result_source",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "source_georeferencing_method",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "georeferencing_confidence",
                table: "compliance_evaluations");

            migrationBuilder.DropColumn(
                name: "is_approximate",
                table: "compliance_evaluations");

            migrationBuilder.DropColumn(
                name: "requires_manual_review",
                table: "compliance_evaluations");

            migrationBuilder.DropColumn(
                name: "result_source",
                table: "compliance_evaluations");

            migrationBuilder.DropColumn(
                name: "source_georeferencing_method",
                table: "compliance_evaluations");
        }
    }
}
