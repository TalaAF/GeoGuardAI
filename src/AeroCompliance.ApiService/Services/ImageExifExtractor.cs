using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace AeroCompliance.ApiService.Services;

/// <summary>
/// Server-extracted (authoritative) capture metadata read from a JPEG's EXIF/GPS tags. Client-side
/// EXIF preview in the browser is a convenience only — this is what the API actually trusts.
/// </summary>
public sealed record ExtractedImageMetadata(
    DateTimeOffset? CapturedAt,
    double? Latitude,
    double? Longitude,
    decimal? AltitudeM,
    decimal? HeadingDeg,
    string? CameraMake,
    string? CameraModel);

/// <summary>
/// Reads EXIF/GPS metadata from JPEG files using the pure-managed <c>MetadataExtractor</c> library.
/// Never throws — a corrupt or absent EXIF segment simply yields fewer fields, it is not an upload
/// failure (the caller falls back to user-provided values or leaves the field missing/"Missing").
/// </summary>
public static class ImageExifExtractor
{
    public static ExtractedImageMetadata? TryExtract(string filePath, ILogger? logger = null)
    {
        try
        {
            IReadOnlyList<MetadataExtractor.Directory> directories;
            using (var stream = File.OpenRead(filePath))
            {
                directories = ImageMetadataReader.ReadMetadata(stream);
            }

            var (latitude, longitude) = ReadGps(directories);
            var altitude = ReadAltitude(directories);
            var heading = ReadHeading(directories);
            var capturedAt = ReadCapturedAt(directories);
            var (make, model) = ReadCamera(directories);

            if (capturedAt is null && latitude is null && altitude is null && heading is null && make is null && model is null)
            {
                return null;
            }

            return new ExtractedImageMetadata(capturedAt, latitude, longitude, altitude, heading, make, model);
        }
        catch (Exception ex)
        {
            // Never let a malformed/truncated EXIF segment fail the upload — just extract nothing.
            logger?.LogDebug(ex, "EXIF extraction failed for {Path}; continuing without it.", filePath);
            return null;
        }
    }

    private static (double? Latitude, double? Longitude) ReadGps(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
        var location = gps?.GetGeoLocation();
        if (location is null || location.IsZero)
        {
            return (null, null);
        }

        var lat = location.Latitude;
        var lon = location.Longitude;
        if (!double.IsFinite(lat) || !double.IsFinite(lon) || lat is < -90 or > 90 || lon is < -180 or > 180)
        {
            return (null, null);
        }

        return (lat, lon);
    }

    private static decimal? ReadAltitude(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
        if (gps is null || !gps.TryGetDouble(GpsDirectory.TagAltitude, out var meters) || !double.IsFinite(meters))
        {
            return null;
        }

        // AltitudeRef 1 = below sea level.
        if (gps.TryGetInt32(GpsDirectory.TagAltitudeRef, out var reference) && reference == 1)
        {
            meters = -meters;
        }

        // Reject implausible altitudes (corrupt EXIF rationals) rather than trusting them blindly.
        return meters is < -500 or > 12000 ? null : (decimal)meters;
    }

    private static decimal? ReadHeading(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
        if (gps is null || !gps.TryGetDouble(GpsDirectory.TagImgDirection, out var degrees) || !double.IsFinite(degrees))
        {
            return null;
        }

        return degrees is < 0 or > 360 ? null : (decimal)degrees;
    }

    private static DateTimeOffset? ReadCapturedAt(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        if (subIfd is null)
        {
            return null;
        }

        if (!subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dateTime) &&
            !subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out dateTime))
        {
            return null;
        }

        if (dateTime <= new DateTime(1990, 1, 1))
        {
            return null;
        }

        // EXIF's DateTimeOriginal has no reliable timezone offset in the vast majority of cameras/drones.
        // Rather than pretending to know the offset, treat the naive timestamp as UTC and let the
        // "Exif" CaptureTimeSource tag make that provenance (and its uncertainty) explicit to reviewers.
        return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
    }

    private static (string? Make, string? Model) ReadCamera(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var make = ifd0 is null ? null : SafeString(ifd0, ExifDirectoryBase.TagMake);
        var model = ifd0 is null ? null : SafeString(ifd0, ExifDirectoryBase.TagModel);
        return (make, model);
    }

    private static string? SafeString(MetadataExtractor.Directory directory, int tag)
    {
        var value = directory.GetDescription(tag)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
