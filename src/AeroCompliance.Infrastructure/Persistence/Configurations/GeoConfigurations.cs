using AeroCompliance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroCompliance.Infrastructure.Persistence.Configurations;

public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> b)
    {
        b.ToTable("zones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.MunicipalityId).HasColumnName("municipality_id").IsRequired();
        b.Property(x => x.ZoneNumber).HasColumnName("zone_number").HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        b.Property(x => x.ZoneType).HasColumnName("zone_type").HasMaxLength(50);
        b.Property(x => x.Geom).HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)").IsRequired();
        b.Property(x => x.SourceDocument).HasColumnName("source_document").HasMaxLength(255);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.MunicipalityId, x.ZoneNumber }).IsUnique();
        b.HasIndex(x => x.Geom).HasMethod("gist");

        b.HasOne(x => x.Municipality)
            .WithMany(m => m.Zones)
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> b)
    {
        b.ToTable("blocks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.ZoneId).HasColumnName("zone_id").IsRequired();
        b.Property(x => x.BlockNumber).HasColumnName("block_number").HasMaxLength(50).IsRequired();
        b.Property(x => x.Geom).HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.ZoneId, x.BlockNumber }).IsUnique();
        b.HasIndex(x => x.Geom).HasMethod("gist");

        b.HasOne(x => x.Zone)
            .WithMany(z => z.Blocks)
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ParcelConfiguration : IEntityTypeConfiguration<Parcel>
{
    public void Configure(EntityTypeBuilder<Parcel> b)
    {
        b.ToTable("parcels");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.BlockId).HasColumnName("block_id").IsRequired();
        b.Property(x => x.ParcelNumber).HasColumnName("parcel_number").HasMaxLength(50).IsRequired();
        b.Property(x => x.OwnerName).HasColumnName("owner_name").HasMaxLength(200);
        b.Property(x => x.ZoneType).HasColumnName("zone_type").HasMaxLength(50);
        b.Property(x => x.AreaM2).HasColumnName("area_m2").HasColumnType("numeric(12,2)");
        b.Property(x => x.Geom).HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)").IsRequired();
        b.Property(x => x.Source).HasColumnName("source").HasMaxLength(30).HasDefaultValue("municipality_gis").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.BlockId, x.ParcelNumber }).IsUnique();
        b.HasIndex(x => x.Geom).HasMethod("gist");

        b.HasOne(x => x.Block)
            .WithMany(z => z.Parcels)
            .HasForeignKey(x => x.BlockId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoadConfiguration : IEntityTypeConfiguration<Road>
{
    public void Configure(EntityTypeBuilder<Road> b)
    {
        b.ToTable("roads");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.MunicipalityId).HasColumnName("municipality_id").IsRequired();
        b.Property(x => x.RoadCode).HasColumnName("road_code").HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        b.Property(x => x.RoadType).HasColumnName("road_type").HasMaxLength(50);
        b.Property(x => x.WidthM).HasColumnName("width_m").HasColumnType("numeric(6,2)");
        b.Property(x => x.SpeedLimitKmh).HasColumnName("speed_limit_kmh");
        b.Property(x => x.IsSensitive).HasColumnName("is_sensitive").HasDefaultValue(false);
        b.Property(x => x.Geom).HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.MunicipalityId, x.RoadCode }).IsUnique();
        b.HasIndex(x => x.Geom).HasMethod("gist");

        b.HasOne(x => x.Municipality)
            .WithMany(m => m.Roads)
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ZoningRuleConfiguration : IEntityTypeConfiguration<ZoningRule>
{
    public void Configure(EntityTypeBuilder<ZoningRule> b)
    {
        b.ToTable("zoning_rules", t =>
            t.HasCheckConstraint("ck_zoning_rules_target", "zone_id IS NOT NULL OR zone_type IS NOT NULL"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        b.Property(x => x.MunicipalityId).HasColumnName("municipality_id").IsRequired();
        b.Property(x => x.ZoneId).HasColumnName("zone_id");
        b.Property(x => x.ZoneType).HasColumnName("zone_type").HasMaxLength(50);
        b.Property(x => x.RuleKey).HasColumnName("rule_key").HasMaxLength(100).IsRequired();
        b.Property(x => x.RuleValue).HasColumnName("rule_value").HasColumnType("numeric(10,3)").IsRequired();
        b.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(20).IsRequired();
        b.Property(x => x.SourceDocument).HasColumnName("source_document").HasMaxLength(255);
        b.Property(x => x.ExtractedByAi).HasColumnName("extracted_by_ai").HasDefaultValue(true);
        b.Property(x => x.IsValidated).HasColumnName("is_validated").HasDefaultValue(false);
        b.Property(x => x.ValidatedBy).HasColumnName("validated_by");
        b.Property(x => x.ValidatedAt).HasColumnName("validated_at");
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(false);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        // Partial index matching: CREATE INDEX ... ON zoning_rules (rule_key) WHERE is_active = true
        b.HasIndex(x => x.RuleKey).HasFilter("is_active = true");
        b.HasIndex(x => x.MunicipalityId);

        b.HasOne(x => x.Municipality)
            .WithMany()
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Zone)
            .WithMany(z => z.ZoningRules)
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Validator)
            .WithMany()
            .HasForeignKey(x => x.ValidatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
