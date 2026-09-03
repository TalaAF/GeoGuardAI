import { parse } from "exifr";

/**
 * Browser-side EXIF preview only — shown to the user immediately after picking a file so they don't
 * have to wait for the upload round-trip to see what the camera recorded. The API independently
 * re-extracts this from the uploaded bytes and is what the system actually trusts (see the root
 * README's "Metadata precedence" section); this preview is never sent to the server as-is.
 */
export interface ExifPreview {
  capturedAt: string | null;
  latitude: number | null;
  longitude: number | null;
  altitudeM: number | null;
  cameraMake: string | null;
  cameraModel: string | null;
}

export async function readExifPreview(file: File): Promise<ExifPreview | null> {
  if (!/^image\/jpe?g$/i.test(file.type)) return null;
  try {
    const tags = await parse(file, {
      pick: ["DateTimeOriginal", "GPSLatitude", "GPSLongitude", "GPSAltitude", "Make", "Model"],
    });
    if (!tags) return null;
    const lat = typeof tags.latitude === "number" ? tags.latitude : null;
    const lon = typeof tags.longitude === "number" ? tags.longitude : null;
    const capturedAt =
      tags.DateTimeOriginal instanceof Date ? tags.DateTimeOriginal.toISOString() : null;
    if (!capturedAt && lat === null && !tags.Make && !tags.Model) return null;
    return {
      capturedAt,
      latitude: lat,
      longitude: lon,
      altitudeM: typeof tags.GPSAltitude === "number" ? tags.GPSAltitude : null,
      cameraMake: typeof tags.Make === "string" ? tags.Make : null,
      cameraModel: typeof tags.Model === "string" ? tags.Model : null,
    };
  } catch {
    // A preview is a convenience, never a requirement — a corrupt/unreadable EXIF segment just
    // means no preview, the same way the server-side extractor degrades.
    return null;
  }
}
