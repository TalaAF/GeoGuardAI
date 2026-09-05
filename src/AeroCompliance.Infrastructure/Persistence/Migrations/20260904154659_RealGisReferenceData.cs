using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AeroCompliance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RealGisReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gis_reference_features",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    municipality_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    source_layer = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    source_feature_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    source_crs = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    geom = table.Column<MultiPolygon>(type: "geometry(MultiPolygon,4326)", nullable: false),
                    area_m2 = table.Column<double>(type: "double precision", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    source_checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    geometry_repaired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gis_reference_features", x => x.id);
                    table.ForeignKey(
                        name: "FK_gis_reference_features_municipalities_municipality_id",
                        column: x => x.municipality_id,
                        principalTable: "municipalities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gis_reference_features_geom",
                table: "gis_reference_features",
                column: "geom")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "ix_gis_reference_features_source_identity",
                table: "gis_reference_features",
                columns: new[] { "source_layer", "source_feature_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gis_reference_features_tenant_type",
                table: "gis_reference_features",
                columns: new[] { "municipality_id", "reference_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gis_reference_features");
        }
    }
}
