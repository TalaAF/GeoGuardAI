using AeroCompliance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroCompliance.Infrastructure.Persistence.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> b)
    {
        b.ToTable("user_notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.MunicipalityId).HasColumnName("municipality_id").IsRequired();
        b.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id").IsRequired();
        b.Property(x => x.NotificationType).HasColumnName("notification_type").HasMaxLength(60).IsRequired();
        b.Property(x => x.Severity).HasColumnName("severity").HasMaxLength(20).IsRequired();
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        b.Property(x => x.Message).HasColumnName("message").HasMaxLength(1000).IsRequired();
        b.Property(x => x.RelatedEntityType).HasColumnName("related_entity_type").HasMaxLength(50);
        b.Property(x => x.RelatedEntityId).HasColumnName("related_entity_id");
        b.Property(x => x.Route).HasColumnName("route").HasMaxLength(300);
        b.Property(x => x.SourceEventKey).HasColumnName("source_event_key").HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("now()");
        b.Property(x => x.ReadAtUtc).HasColumnName("read_at_utc");
        b.Property(x => x.DismissedAtUtc).HasColumnName("dismissed_at_utc");

        // Idempotency: redelivery of the same event to the same user is a no-op rather than a duplicate.
        b.HasIndex(x => new { x.RecipientUserId, x.NotificationType, x.SourceEventKey })
            .IsUnique()
            .HasDatabaseName("ix_user_notifications_idempotency");

        // Drives the default list (newest first, per recipient) and the unread badge.
        b.HasIndex(x => new { x.RecipientUserId, x.DismissedAtUtc, x.CreatedAtUtc })
            .HasDatabaseName("ix_user_notifications_recipient_feed");
        b.HasIndex(x => new { x.RecipientUserId, x.ReadAtUtc })
            .HasDatabaseName("ix_user_notifications_unread");

        b.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RecipientUser).WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> b)
    {
        b.ToTable("notification_preferences");
        // The user owns exactly one preference row, so the user id is the primary key.
        b.HasKey(x => x.UserId);
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.CandidateViolations).HasColumnName("candidate_violations").HasDefaultValue(true);
        b.Property(x => x.ProcessingFailures).HasColumnName("processing_failures").HasDefaultValue(true);
        b.Property(x => x.ReviewOutcomes).HasColumnName("review_outcomes").HasDefaultValue(true);
        b.Property(x => x.GeneratedReports).HasColumnName("generated_reports").HasDefaultValue(true);
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasDefaultValueSql("now()");

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
