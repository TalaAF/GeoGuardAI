using AeroCompliance.Domain.Entities;
using AeroCompliance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroCompliance.Infrastructure.Persistence.Configurations;

public class FlightConfiguration : IEntityTypeConfiguration<Flight>
{
    public void Configure(EntityTypeBuilder<Flight> b)
    {
        b.ToTable("flights");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.MunicipalityId).HasColumnName("municipality_id").IsRequired();
        b.Property(x => x.DroneOperatorId).HasColumnName("drone_operator_id").IsRequired();
        b.Property(x => x.MissionName).HasColumnName("mission_name").HasMaxLength(200);
        b.Property(x => x.PlannedArea).HasColumnName("planned_area").HasColumnType("geometry(MultiPolygon, 4326)");

        // status is stored as its snake_case string value ('planned', 'in_progress', ...).
        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasDefaultValue(FlightStatus.Planned)
            .HasConversion(
                v => FlightStatusToDb(v),
                v => FlightStatusFromDb(v))
            .IsRequired();

        b.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.EndedAt).HasColumnName("ended_at");
        b.Property(x => x.WeatherConditions).HasColumnName("weather_conditions").HasColumnType("jsonb");
        b.Property(x => x.QualityFlag).HasColumnName("quality_flag").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(x => x.PlannedArea).HasMethod("gist");

        b.HasOne(x => x.Municipality)
            .WithMany(m => m.Flights)
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.DroneOperator)
            .WithMany()
            .HasForeignKey(x => x.DroneOperatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string FlightStatusToDb(FlightStatus status) => status switch
    {
        FlightStatus.Planned => "planned",
        FlightStatus.InProgress => "in_progress",
        FlightStatus.Completed => "completed",
        FlightStatus.Aborted => "aborted",
        FlightStatus.FlaggedLowQuality => "flagged_low_quality",
        _ => "planned"
    };

    private static FlightStatus FlightStatusFromDb(string value) => value switch
    {
        "planned" => FlightStatus.Planned,
        "in_progress" => FlightStatus.InProgress,
        "completed" => FlightStatus.Completed,
        "aborted" => FlightStatus.Aborted,
        "flagged_low_quality" => FlightStatus.FlaggedLowQuality,
        _ => FlightStatus.Planned
    };
}

public class FlightMissionConfiguration : IEntityTypeConfiguration<FlightMission>
{
    public void Configure(EntityTypeBuilder<FlightMission> b)
    {
        b.ToTable("flight_missions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.FlightId).HasColumnName("flight_id").IsRequired();
        b.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(50);
        b.Property(x => x.AltitudeM).HasColumnName("altitude_m").HasColumnType("numeric(6,2)");
        b.Property(x => x.OverlapPercent).HasColumnName("overlap_percent").HasColumnType("numeric(5,2)");
        b.Property(x => x.Notes).HasColumnName("notes");

        b.HasOne(x => x.Flight)
            .WithMany(f => f.Missions)
            .HasForeignKey(x => x.FlightId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ImageConfiguration : IEntityTypeConfiguration<Image>
{
    public void Configure(EntityTypeBuilder<Image> b)
    {
        b.ToTable("images");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.FlightId).HasColumnName("flight_id").IsRequired();
        b.Property(x => x.StoragePath).HasColumnName("storage_path").IsRequired();
        b.Property(x => x.Bucket).HasColumnName("bucket").HasMaxLength(100).IsRequired();
        b.Property(x => x.ObjectKey).HasColumnName("object_key").HasMaxLength(1024).IsRequired();
        b.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        b.Property(x => x.ByteLength).HasColumnName("byte_length").IsRequired();
        b.Property(x => x.Sha256Hash).HasColumnName("sha256_hash").HasColumnType("char(64)").IsRequired();
        b.Property(x => x.CapturedAt).HasColumnName("captured_at").IsRequired();
        b.Property(x => x.Latitude).HasColumnName("latitude");
        b.Property(x => x.Longitude).HasColumnName("longitude");
        b.Property(x => x.AltitudeM).HasColumnName("altitude_m").HasColumnType("numeric(8,2)");
        b.Property(x => x.CameraHeadingDeg).HasColumnName("camera_heading_deg").HasColumnType("numeric(6,2)");
        b.Property(x => x.GpsAccuracyCm).HasColumnName("gps_accuracy_cm").HasColumnType("numeric(6,2)");

        // GENERATED ALWAYS AS (ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)) STORED.
        // Mapped as a computed (stored) column: the database owns the value; EF never writes it.
        b.Property(x => x.Geom)
            .HasColumnName("geom")
            .HasColumnType("geometry(Point, 4326)")
            .HasComputedColumnSql("ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)", stored: true);

        b.Property(x => x.IsPartOfOrtho).HasColumnName("is_part_of_ortho").HasDefaultValue(false);
        b.Property(x => x.UploadCompletedAt).HasColumnName("upload_completed_at");
        b.Property(x => x.ProcessingStatus).HasColumnName("processing_status").HasMaxLength(40).HasConversion<string>().IsRequired();
        b.Property(x => x.ProcessingRequestId).HasColumnName("processing_request_id");
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        b.Property(x => x.QueuedAt).HasColumnName("queued_at");
        b.Property(x => x.ProcessingStartedAt).HasColumnName("processing_started_at");
        b.Property(x => x.ProcessingCompletedAt).HasColumnName("processing_completed_at");
        b.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(100);
        b.Property(x => x.LastErrorMessage).HasColumnName("last_error_message").HasMaxLength(1000);
        b.Property(x => x.SourceRasterCrs).HasColumnName("source_raster_crs").HasMaxLength(100);
        b.Property(x => x.RasterWidth).HasColumnName("raster_width");
        b.Property(x => x.RasterHeight).HasColumnName("raster_height");
        b.Property(x => x.AffineTransform).HasColumnName("affine_transform").HasColumnType("jsonb");
        b.Property(x => x.LocationSource).HasColumnName("location_source").HasMaxLength(20).HasDefaultValue("Missing").IsRequired();
        b.Property(x => x.CaptureTimeSource).HasColumnName("capture_time_source").HasMaxLength(20).HasDefaultValue("Missing").IsRequired();
        b.Property(x => x.MetadataRequiresReview).HasColumnName("metadata_requires_review").HasDefaultValue(false);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(x => x.Geom).HasMethod("gist");
        b.HasIndex(x => x.FlightId);
        b.HasIndex(x => x.ProcessingRequestId).IsUnique();

        b.HasOne(x => x.Flight)
            .WithMany(f => f.Images)
            .HasForeignKey(x => x.FlightId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> b)
    {
        b.ToTable("videos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.FlightId).HasColumnName("flight_id").IsRequired();
        b.Property(x => x.StoragePath).HasColumnName("storage_path").IsRequired();
        b.Property(x => x.Sha256Hash).HasColumnName("sha256_hash").HasColumnType("char(64)").IsRequired();
        b.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
        b.Property(x => x.EndedAt).HasColumnName("ended_at");
        b.Property(x => x.DurationSeconds).HasColumnName("duration_seconds");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasOne(x => x.Flight)
            .WithMany(f => f.Videos)
            .HasForeignKey(x => x.FlightId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrthomosaicConfiguration : IEntityTypeConfiguration<Orthomosaic>
{
    public void Configure(EntityTypeBuilder<Orthomosaic> b)
    {
        b.ToTable("orthomosaics");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.FlightId).HasColumnName("flight_id").IsRequired();
        b.Property(x => x.StoragePath).HasColumnName("storage_path").IsRequired();
        b.Property(x => x.Bounds).HasColumnName("bounds").HasColumnType("geometry(Polygon, 4326)").IsRequired();
        b.Property(x => x.ResolutionCmPerPx).HasColumnName("resolution_cm_per_px").HasColumnType("numeric(6,2)");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(x => x.Bounds).HasMethod("gist");

        b.HasOne(x => x.Flight)
            .WithMany(f => f.Orthomosaics)
            .HasForeignKey(x => x.FlightId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
