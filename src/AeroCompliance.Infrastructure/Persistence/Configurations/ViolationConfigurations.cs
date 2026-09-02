using AeroCompliance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroCompliance.Infrastructure.Persistence.Configurations;

public class DetectionConfiguration : IEntityTypeConfiguration<Detection>
{
    public void Configure(EntityTypeBuilder<Detection> b)
    {
        b.ToTable("detections", t =>
        {
            t.HasCheckConstraint("ck_detections_source", "orthomosaic_id IS NOT NULL OR image_id IS NOT NULL");
            t.HasCheckConstraint("ck_detections_confidence", "confidence BETWEEN 0 AND 1");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.ImageAnalysisId).HasColumnName("image_analysis_id");
        b.Property(x => x.ExternalDetectionId).HasColumnName("external_detection_id");
        b.Property(x => x.ClassId).HasColumnName("class_id");
        b.Property(x => x.OrthomosaicId).HasColumnName("orthomosaic_id");
        b.Property(x => x.ImageId).HasColumnName("image_id");
        b.Property(x => x.ClassLabel).HasColumnName("class_label").HasMaxLength(50).IsRequired();
        b.Property(x => x.Confidence).HasColumnName("confidence").HasColumnType("numeric(5,4)").IsRequired();
        b.Property(x => x.Geom).HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)").IsRequired();
        b.Property(x => x.AreaM2).HasColumnName("area_m2").HasColumnType("numeric(12,2)");
        b.Property(x => x.PerimeterM).HasColumnName("perimeter_m").HasColumnType("numeric(12,2)");
        b.Property(x => x.HeightM).HasColumnName("height_m").HasColumnType("numeric(8,2)");
        b.Property(x => x.ModelName).HasColumnName("model_name").HasMaxLength(100).IsRequired();
        b.Property(x => x.ModelVersion).HasColumnName("model_version").HasMaxLength(50).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(x => x.Geom).HasMethod("gist");
        b.HasIndex(x => x.ClassLabel);
        b.HasIndex(x => new { x.ImageAnalysisId, x.ExternalDetectionId }).IsUnique();

        b.HasOne(x => x.ImageAnalysis)
            .WithMany(x => x.Detections)
            .HasForeignKey(x => x.ImageAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Orthomosaic)
            .WithMany(o => o.Detections)
            .HasForeignKey(x => x.OrthomosaicId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Image)
            .WithMany()
            .HasForeignKey(x => x.ImageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ViolationStatusConfiguration : IEntityTypeConfiguration<ViolationStatus>
{
    public void Configure(EntityTypeBuilder<ViolationStatus> b)
    {
        b.ToTable("violation_statuses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
        b.Property(x => x.LabelAr).HasColumnName("label_ar").HasMaxLength(100);
        b.Property(x => x.LabelEn).HasColumnName("label_en").HasMaxLength(100);

        b.HasIndex(x => x.Code).IsUnique();

        // Seed the reference statuses (matches the INSERT in the SQL schema, in the same order/id).
        b.HasData(
            new ViolationStatus { Id = 1, Code = "under_review", LabelAr = "بانتظار المراجعة", LabelEn = "Under review" },
            new ViolationStatus { Id = 2, Code = "confirmed_under_notification", LabelAr = "مؤكدة - جاري الإخطار", LabelEn = "Confirmed – under notification" },
            new ViolationStatus { Id = 3, Code = "notified", LabelAr = "تم الإخطار", LabelEn = "Notified" },
            new ViolationStatus { Id = 4, Code = "under_dispute", LabelAr = "تحت الاعتراض", LabelEn = "Under dispute/appeal" },
            new ViolationStatus { Id = 5, Code = "paused", LabelAr = "متوقفة مؤقتًا", LabelEn = "Paused" },
            new ViolationStatus { Id = 6, Code = "resolved", LabelAr = "تمت المعالجة", LabelEn = "Removed / resolved" },
            new ViolationStatus { Id = 7, Code = "dismissed", LabelAr = "مرفوضة", LabelEn = "Dismissed" });
    }
}

public class ViolationConfiguration : IEntityTypeConfiguration<Violation>
{
    public void Configure(EntityTypeBuilder<Violation> b)
    {
        b.ToTable("violations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.ComplianceEvaluationId).HasColumnName("compliance_evaluation_id");
        b.Property(x => x.DetectionId).HasColumnName("detection_id").IsRequired();
        b.Property(x => x.ParcelId).HasColumnName("parcel_id");
        b.Property(x => x.RoadId).HasColumnName("road_id");
        b.Property(x => x.ZoneId).HasColumnName("zone_id");
        b.Property(x => x.BlockId).HasColumnName("block_id");
        b.Property(x => x.RuleId).HasColumnName("rule_id");
        b.Property(x => x.ViolationType).HasColumnName("violation_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.MeasuredValue).HasColumnName("measured_value").HasColumnType("numeric(12,3)");
        b.Property(x => x.RequiredValue).HasColumnName("required_value").HasColumnType("numeric(12,3)");
        b.Property(x => x.ExceedanceValue).HasColumnName("exceedance_value").HasColumnType("numeric(12,3)");
        b.Property(x => x.ExceedanceAreaM2).HasColumnName("exceedance_area_m2").HasColumnType("numeric(12,2)");
        b.Property(x => x.AiConfidenceScore).HasColumnName("ai_confidence_score").HasColumnType("numeric(5,4)").IsRequired();
        b.Property(x => x.IsZeroState).HasColumnName("is_zero_state").HasDefaultValue(false);
        b.Property(x => x.CurrentStatusId).HasColumnName("current_status_id").HasDefaultValue(1).IsRequired();
        b.Property(x => x.EvidenceImageId).HasColumnName("evidence_image_id");
        b.Property(x => x.Geom).HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        b.HasIndex(x => x.Geom).HasMethod("gist");
        b.HasIndex(x => x.CurrentStatusId);
        b.HasIndex(x => x.ParcelId);
        b.HasIndex(x => x.ComplianceEvaluationId).IsUnique();

        b.HasOne(x => x.ComplianceEvaluation).WithOne(x => x.Violation)
            .HasForeignKey<Violation>(x => x.ComplianceEvaluationId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Detection).WithMany().HasForeignKey(x => x.DetectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Parcel).WithMany().HasForeignKey(x => x.ParcelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Road).WithMany().HasForeignKey(x => x.RoadId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Block).WithMany().HasForeignKey(x => x.BlockId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Rule).WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CurrentStatus).WithMany(s => s.Violations).HasForeignKey(x => x.CurrentStatusId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.EvidenceImage).WithMany().HasForeignKey(x => x.EvidenceImageId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ViolationStatusHistoryConfiguration : IEntityTypeConfiguration<ViolationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ViolationStatusHistory> b)
    {
        b.ToTable("violation_status_history");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.ViolationId).HasColumnName("violation_id").IsRequired();
        b.Property(x => x.StatusId).HasColumnName("status_id").IsRequired();
        b.Property(x => x.ChangedBy).HasColumnName("changed_by");
        b.Property(x => x.Notes).HasColumnName("notes");
        b.Property(x => x.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("now()");

        b.HasIndex(x => x.ViolationId);

        b.HasOne(x => x.Violation)
            .WithMany(v => v.StatusHistory)
            .HasForeignKey(x => x.ViolationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Status)
            .WithMany(s => s.History)
            .HasForeignKey(x => x.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
