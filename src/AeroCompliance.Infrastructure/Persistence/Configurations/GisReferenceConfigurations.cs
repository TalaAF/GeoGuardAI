using AeroCompliance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroCompliance.Infrastructure.Persistence.Configurations;

public sealed class GisReferenceFeatureConfiguration : IEntityTypeConfiguration<GisReferenceFeature>
{
    public void Configure(EntityTypeBuilder<GisReferenceFeature> b)
    {
        b.ToTable("gis_reference_features");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.MunicipalityId).HasColumnName("municipality_id").IsRequired();
        b.Property(x => x.ReferenceType).HasColumnName("reference_type").HasMaxLength(60).IsRequired();
        b.Property(x => x.SourceLayer).HasColumnName("source_layer").HasMaxLength(120).IsRequired();
        b.Property(x => x.SourceFeatureId).HasColumnName("source_feature_id").HasMaxLength(120).IsRequired();
        b.Property(x => x.SourceCrs).HasColumnName("source_crs").HasMaxLength(40).IsRequired();
        b.Property(x => x.Geom).HasColumnName("geom").HasColumnType("geometry(MultiPolygon,4326)").IsRequired();
        b.Property(x => x.AreaM2).HasColumnName("area_m2");
        b.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
        b.Property(x => x.SourceChecksum).HasColumnName("source_checksum").HasMaxLength(64).IsRequired();
        b.Property(x => x.GeometryRepaired).HasColumnName("geometry_repaired").HasDefaultValue(false);
        b.Property(x => x.ImportedAt).HasColumnName("imported_at").HasDefaultValueSql("now()");

        // Natural key: re-importing the same layer updates its rows instead of duplicating them.
        b.HasIndex(x => new { x.SourceLayer, x.SourceFeatureId })
            .IsUnique()
            .HasDatabaseName("ix_gis_reference_features_source_identity");

        b.HasIndex(x => new { x.MunicipalityId, x.ReferenceType })
            .HasDatabaseName("ix_gis_reference_features_tenant_type");

        // Spatial index: every use of this table is an intersection test against a detection.
        b.HasIndex(x => x.Geom)
            .HasMethod("GIST")
            .HasDatabaseName("ix_gis_reference_features_geom");

        b.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
