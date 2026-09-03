using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroCompliance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5ImageMetadataProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "capture_time_source",
                table: "images",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Missing");

            migrationBuilder.AddColumn<string>(
                name: "location_source",
                table: "images",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Missing");

            migrationBuilder.AddColumn<bool>(
                name: "metadata_requires_review",
                table: "images",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "capture_time_source",
                table: "images");

            migrationBuilder.DropColumn(
                name: "location_source",
                table: "images");

            migrationBuilder.DropColumn(
                name: "metadata_requires_review",
                table: "images");
        }
    }
}
