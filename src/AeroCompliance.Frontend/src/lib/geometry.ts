import turfArea from "@turf/area";
import turfBooleanWithin from "@turf/boolean-within";
import { polygon as turfPolygonFeature } from "@turf/helpers";
import turfKinks from "@turf/kinks";

/**
 * Geometry normalization/validation for the GIS/flight boundary editors.
 *
 * Everything here works in plain GeoJSON (`[longitude, latitude]`) — the only place latitude comes
 * first is `RAMALLAH_CENTER`, because that is what Leaflet's `map.setView([lat, lon], zoom)` expects.
 * `GeometryEditor` reads/writes shapes exclusively through Leaflet's own `L.geoJSON()` /
 * `layer.toGeoJSON()`, which already do the `[lat,lon]` <-> `[lon,lat]` conversion — nothing in this
 * module or its caller ever has to reorder a coordinate pair by hand.
 */

export type Position = [number, number];
export type Ring = Position[];
export type PolygonCoordinates = Ring[]; // exterior ring first, then holes
export interface NormalizedMultiPolygon {
  type: "MultiPolygon";
  coordinates: PolygonCoordinates[];
}

export type NormalizeResult =
  | { ok: true; geometry: NormalizedMultiPolygon; areaM2: number; partCount: number }
  | { ok: false; error: string };

const MIN_RING_AREA_M2 = 0.5; // guards against degenerate/collinear rings, not a real minimum boundary size

function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function isValidPosition(value: unknown): value is Position {
  return (
    Array.isArray(value) &&
    value.length >= 2 &&
    isFiniteNumber(value[0]) &&
    isFiniteNumber(value[1]) &&
    value[0] >= -180 &&
    value[0] <= 180 &&
    value[1] >= -90 &&
    value[1] <= 90
  );
}

function closeRing(ring: Position[]): Position[] {
  if (ring.length === 0) return ring;
  const [fx, fy] = ring[0];
  const [lx, ly] = ring[ring.length - 1];
  return fx === lx && fy === ly ? ring : [...ring, [fx, fy]];
}

/** Validates one ring (exterior or hole) and returns it closed, or an error string. */
function validateRing(raw: unknown, where: string): Ring | string {
  if (!Array.isArray(raw) || raw.length === 0) {
    return `${where}: expected a ring of [longitude, latitude] points.`;
  }
  const positions: Position[] = [];
  for (const point of raw) {
    if (!isValidPosition(point)) {
      return `${where}: found a coordinate that isn't a finite longitude/latitude pair within valid ranges (-180..180, -90..90).`;
    }
    positions.push([point[0], point[1]]);
  }
  const closed = closeRing(positions);
  if (closed.length < 4) {
    return `${where}: a boundary needs at least 3 distinct points.`;
  }
  return closed;
}

/** Validates one polygon's ring set (already individually ring-validated) for self-intersection and area. */
function validatePolygonPart(rings: PolygonCoordinates, where: string): string | null {
  let feature;
  try {
    feature = turfPolygonFeature(rings);
  } catch (e) {
    return `${where}: ${e instanceof Error ? e.message : "invalid polygon."}`;
  }
  let kinks;
  try {
    kinks = turfKinks(feature);
  } catch {
    kinks = { features: [] as unknown[] };
  }
  if (kinks.features.length > 0) {
    return `${where}: the boundary crosses itself (self-intersecting). Redraw it without crossing lines, or fix it in the source file.`;
  }
  const area = turfArea(feature);
  if (!Number.isFinite(area) || area < MIN_RING_AREA_M2) {
    return `${where}: the boundary has no measurable area — its points may be collinear or duplicated.`;
  }
  return null;
}

/** Extracts every Polygon/MultiPolygon geometry object out of a raw GeoJSON value (geometry, Feature, or FeatureCollection). */
function collectGeometries(input: unknown, out: any[], rejectedTypes: Set<string>): void {
  if (!input || typeof input !== "object") return;
  const obj = input as Record<string, unknown>;
  switch (obj.type) {
    case "FeatureCollection":
      for (const f of (obj.features as unknown[]) ?? []) collectGeometries(f, out, rejectedTypes);
      return;
    case "Feature":
      collectGeometries(obj.geometry, out, rejectedTypes);
      return;
    case "Polygon":
    case "MultiPolygon":
      out.push(obj);
      return;
    default:
      if (typeof obj.type === "string") rejectedTypes.add(obj.type);
  }
}

/**
 * Parses/validates arbitrary GeoJSON input (a raw geometry, a Feature, or a FeatureCollection) and
 * returns a single normalized MultiPolygon, or a clear error. Every valid part is preserved — nothing
 * is silently dropped; a single malformed/self-intersecting part rejects the whole submission (with
 * a message naming which part) rather than silently repairing or discarding it, since this is
 * user-authored data, not machine-generated detection output.
 */
export function normalizeGeometryInput(input: unknown): NormalizeResult {
  let parsed = input;
  if (typeof input === "string") {
    try {
      parsed = JSON.parse(input);
    } catch {
      return { ok: false, error: "That isn't valid JSON." };
    }
  }

  const geometries: any[] = [];
  const rejectedTypes = new Set<string>();
  collectGeometries(parsed, geometries, rejectedTypes);

  if (geometries.length === 0) {
    if (rejectedTypes.size > 0) {
      return {
        ok: false,
        error: `Only Polygon/MultiPolygon boundaries are supported here — found ${[...rejectedTypes].join(", ")} instead.`,
      };
    }
    return { ok: false, error: "No Polygon or MultiPolygon geometry was found." };
  }
  if (rejectedTypes.size > 0) {
    return {
      ok: false,
      error: `This file mixes boundary polygons with unsupported geometry (${[...rejectedTypes].join(", ")}) — remove those features and try again.`,
    };
  }

  const parts: PolygonCoordinates[] = [];
  let partIndex = 0;
  for (const geometry of geometries) {
    const polygonsInThisGeometry: unknown[] =
      geometry.type === "Polygon" ? [geometry.coordinates] : geometry.coordinates;
    if (!Array.isArray(polygonsInThisGeometry)) {
      return { ok: false, error: "Malformed geometry: coordinates are missing or not an array." };
    }
    for (const rawPolygon of polygonsInThisGeometry) {
      partIndex += 1;
      const label = `Boundary part ${partIndex}`;
      if (!Array.isArray(rawPolygon) || rawPolygon.length === 0) {
        return { ok: false, error: `${label}: missing an exterior ring.` };
      }
      const rings: PolygonCoordinates = [];
      for (let i = 0; i < rawPolygon.length; i++) {
        const ringLabel = i === 0 ? `${label} exterior ring` : `${label} hole ${i}`;
        const result = validateRing(rawPolygon[i], ringLabel);
        if (typeof result === "string") return { ok: false, error: result };
        rings.push(result);
      }
      const partError = validatePolygonPart(rings, label);
      if (partError) return { ok: false, error: partError };
      parts.push(rings);
    }
  }

  if (parts.length === 0) {
    return { ok: false, error: "No usable boundary polygon was found." };
  }

  const geometry: NormalizedMultiPolygon = { type: "MultiPolygon", coordinates: parts };
  const areaM2 = totalAreaM2(geometry);
  return { ok: true, geometry, areaM2, partCount: parts.length };
}

export function totalAreaM2(geometry: NormalizedMultiPolygon): number {
  return geometry.coordinates.reduce((sum, rings) => {
    try {
      return sum + turfArea(turfPolygonFeature(rings));
    } catch {
      return sum;
    }
  }, 0);
}

export function formatArea(areaM2: number): string {
  if (areaM2 >= 1_000_000) return `${(areaM2 / 1_000_000).toFixed(2)} km²`;
  if (areaM2 >= 10_000) return `${(areaM2 / 10_000).toFixed(2)} ha`;
  return `${Math.round(areaM2).toLocaleString()} m²`;
}

/**
 * Frontend-only usability check: is `child` entirely within `parent`? This is not a data-integrity
 * guarantee — see the caller for why. `checked: false` means the geometry was too complex for the
 * in-browser check to evaluate confidently (never treated as a hard failure).
 */
export function checkWithin(
  child: NormalizedMultiPolygon,
  parent: unknown,
): { checked: boolean; within: boolean } {
  try {
    const parentGeometries: any[] = [];
    collectGeometries(parent, parentGeometries, new Set());
    if (parentGeometries.length === 0) return { checked: false, within: true };
    const parentFeature = { type: "Feature" as const, properties: {}, geometry: parentGeometries[0] };
    const childFeature = { type: "Feature" as const, properties: {}, geometry: child };
    return { checked: true, within: turfBooleanWithin(childFeature as any, parentFeature as any) };
  } catch {
    return { checked: false, within: true };
  }
}

/** Ramallah city centre — the default map view when there is no existing/parent geometry to zoom to. */
export const RAMALLAH_CENTER: [number, number] = [31.9038, 35.2034];
export const RAMALLAH_ZOOM = 14;

export const MAX_IMPORT_FILE_BYTES = 2 * 1024 * 1024; // 2 MB — generous for a boundary file, not a data dump
