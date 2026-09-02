using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroCompliance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3TenantScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "municipality_id",
                table: "zoning_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "municipality_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE zoning_rules AS rules
                SET municipality_id = zones.municipality_id
                FROM zones
                WHERE rules.zone_id = zones.id;

                DO $$
                DECLARE
                    tenant_count integer;
                    only_tenant uuid;
                BEGIN
                    IF EXISTS (SELECT 1 FROM zoning_rules WHERE municipality_id IS NULL) THEN
                        SELECT COUNT(*), MIN(id) INTO tenant_count, only_tenant FROM municipalities;
                        IF tenant_count <> 1 THEN
                            RAISE EXCEPTION 'Cannot infer municipality for existing zone-type zoning rules; assign them before applying Phase3TenantScoping.';
                        END IF;
                        UPDATE zoning_rules SET municipality_id = only_tenant WHERE municipality_id IS NULL;
                    END IF;
                END $$;

                UPDATE audit_logs AS logs
                SET municipality_id = users.municipality_id
                FROM users
                WHERE logs.actor_id = users.id AND logs.municipality_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "municipality_id",
                table: "zoning_rules",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_zoning_rules_municipality_id",
                table: "zoning_rules",
                column: "municipality_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_municipality_id",
                table: "audit_logs",
                column: "municipality_id");

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_municipalities_municipality_id",
                table: "audit_logs",
                column: "municipality_id",
                principalTable: "municipalities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_zoning_rules_municipalities_municipality_id",
                table: "zoning_rules",
                column: "municipality_id",
                principalTable: "municipalities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_municipalities_municipality_id",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_zoning_rules_municipalities_municipality_id",
                table: "zoning_rules");

            migrationBuilder.DropIndex(
                name: "IX_zoning_rules_municipality_id",
                table: "zoning_rules");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_municipality_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "municipality_id",
                table: "zoning_rules");

            migrationBuilder.DropColumn(
                name: "municipality_id",
                table: "audit_logs");
        }
    }
}
