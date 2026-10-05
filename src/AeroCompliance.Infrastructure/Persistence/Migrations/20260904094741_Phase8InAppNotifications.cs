using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroCompliance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8InAppNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_violations = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    processing_failures = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    review_outcomes = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    generated_reports = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_notification_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    municipality_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    related_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    route = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    source_event_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    read_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dismissed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_notifications_municipalities_municipality_id",
                        column: x => x.municipality_id,
                        principalTable: "municipalities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_notifications_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_notifications_idempotency",
                table: "user_notifications",
                columns: new[] { "recipient_user_id", "notification_type", "source_event_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_municipality_id",
                table: "user_notifications",
                column: "municipality_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_notifications_recipient_feed",
                table: "user_notifications",
                columns: new[] { "recipient_user_id", "dismissed_at_utc", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_user_notifications_unread",
                table: "user_notifications",
                columns: new[] { "recipient_user_id", "read_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "user_notifications");
        }
    }
}
