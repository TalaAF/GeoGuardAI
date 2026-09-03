import type { MetadataSource } from "../api/types";

/** How an image's capture location/time was actually established — shown to end users, never the
 * raw backend code by itself. */
export function locationSourceLabel(source: MetadataSource | string): string {
  switch (source) {
    case "GeoTiff":
      return "GeoTIFF";
    case "Exif":
      return "Drone metadata";
    case "UserProvided":
      return "Selected on map";
    case "Missing":
      return "Missing";
    default:
      return source;
  }
}

export function captureTimeSourceLabel(source: MetadataSource | string): string {
  switch (source) {
    case "Exif":
      return "Drone metadata";
    case "UserProvided":
      return "Entered manually";
    case "Missing":
      return "Missing (using upload time)";
    default:
      return source;
  }
}

/** A short 8-character reference for support purposes only — never the primary label for a record. */
export function shortRef(id: string | null | undefined): string {
  return id ? id.slice(0, 8).toUpperCase() : "—";
}

/** snake_case/space-separated codes ("under_review", "road_encroachment") -> "under review". */
export function humanizeCode(value: string): string {
  return value.replaceAll("_", " ");
}
