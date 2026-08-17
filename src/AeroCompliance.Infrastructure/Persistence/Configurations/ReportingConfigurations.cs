using AeroCompliance.Domain.Entities;
using AeroCompliance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroCompliance.Infrastructure.Persistence.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> b)
    {
        b.ToTable("reports");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.ViolationId).HasColumnName("violation_id").IsRequired();
        b.Property(x => x.StoragePath).HasColumnName("storage_path").IsRequired();
        b.Property(x => x.GeneratedBy).HasColumnName("generated_by").HasMaxLength(30).HasDefaultValue("ai_draft").IsRequired();
        b.Property(x => x.GeneratedAt).HasColumnName("generated_at").HasDefaultValueSql("now()");
        b.Property(x => x.SignedOffBy).HasColumnName("signed_off_by");
        b.Property(x => x.SignedOffAt).HasColumnName("signed_off_at");

        b.HasOne(x => x.Violation)
            .WithMany(v => v.Reports)
            .HasForeignKey(x => x.ViolationId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.SignedOffByUser)
            .WithMany()
            .HasForeignKey(x => x.SignedOffBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.ViolationId).HasColumnName("violation_id");
        b.Property(x => x.RecipientType).HasColumnName("recipient_type").HasMaxLength(30).IsRequired();
        b.Property(x => x.RecipientRef).HasColumnName("recipient_ref").HasMaxLength(200);
        b.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(30).IsRequired();
        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasDefaultValue(NotificationStatus.Pending)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<NotificationStatus>(v, ignoreCase: true))
            .IsRequired();
        b.Property(x => x.SentAt).HasColumnName("sent_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasOne(x => x.Violation)
            .WithMany()
            .HasForeignKey(x => x.ViolationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PoliceAlertConfiguration : IEntityTypeConfiguration<PoliceAlert>
{
    public void Configure(EntityTypeBuilder<PoliceAlert> b)
    {
        b.ToTable("police_alerts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.DetectionId).HasColumnName("detection_id").IsRequired();
        b.Property(x => x.RoadId).HasColumnName("road_id").IsRequired();
        b.Property(x => x.AlertType).HasColumnName("alert_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(30).IsRequired();
        b.Property(x => x.SentAt).HasColumnName("sent_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasOne(x => x.Detection).WithMany().HasForeignKey(x => x.DetectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Road).WithMany().HasForeignKey(x => x.RoadId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn(); // BIGSERIAL
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.EntityId).HasColumnName("entity_id");
        b.Property(x => x.BeforeState).HasColumnName("before_state").HasColumnType("jsonb");
        b.Property(x => x.AfterState).HasColumnName("after_state").HasColumnType("jsonb");
        b.Property(x => x.IpAddress).HasColumnName("ip_address").HasColumnType("inet");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.ActorId);

        b.HasOne(x => x.Actor)
            .WithMany()
            .HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
