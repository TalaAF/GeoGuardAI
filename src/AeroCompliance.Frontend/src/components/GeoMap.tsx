import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { useEffect, useRef } from "react";

/**
 * Thin imperative wrapper around Leaflet (not react-leaflet, to avoid a peer-dependency mismatch
 * against React 19). Renders point markers and/or GeoJSON polygon features, and — when `onPick` is
 * given — lets the viewer click the map to choose a point (used as the manual/map fallback for a
 * missing image location).
 *
 * Geometry is read straight from the API's GeoJSON (`[lon, lat]` order preserved throughout); this
 * component never reprojects or reinterprets coordinates.
 */
export type FeatureKind =
  | "detection"
  | "candidate"
  | "confirmed"
  | "dismissed"
  | "road"
  | "parcel";

export interface MapPoint {
  id: string;
  lat: number;
  lon: number;
  label?: string;
  kind?: "image";
}
export interface MapFeature {
  id: string;
  geometry: unknown;
  kind: FeatureKind;
  label?: string;
}

const STYLE: Record<FeatureKind, L.PathOptions> = {
  detection: { color: "#2563eb", weight: 2, fillOpacity: 0.08 },
  candidate: { color: "#c2410c", weight: 2, fillOpacity: 0.15 },
  confirmed: { color: "#b91c1c", weight: 2, fillOpacity: 0.25 },
  dismissed: { color: "#64748b", weight: 1, fillOpacity: 0.05, dashArray: "4 3" },
  road: { color: "#1e293b", weight: 2, fillOpacity: 0.1 },
  parcel: { color: "#16a34a", weight: 1, fillOpacity: 0.08 },
};
const LEGEND: Array<[FeatureKind, string]> = [
  ["detection", "AI detection"],
  ["candidate", "Candidate violation"],
  ["confirmed", "Confirmed violation"],
  ["dismissed", "Dismissed violation"],
  ["road", "Road"],
  ["parcel", "Parcel"],
];

export default function GeoMap({
  points = [],
  features = [],
  onPick,
  pickedPoint,
  height = 280,
  center,
  legend = false,
}: {
  points?: MapPoint[];
  features?: MapFeature[];
  onPick?: (lat: number, lon: number) => void;
  pickedPoint?: { lat: number; lon: number } | null;
  height?: number;
  center?: [number, number];
  legend?: boolean;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<L.Map | null>(null);
  const layerRef = useRef<L.LayerGroup | null>(null);
  const pickRef = useRef<L.CircleMarker | null>(null);
  const onPickRef = useRef(onPick);
  onPickRef.current = onPick;

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;
    const map = L.map(containerRef.current, { attributionControl: false }).setView(
      center ?? [31.9, 35.2],
      13,
    );
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
    }).addTo(map);
    layerRef.current = L.layerGroup().addTo(map);
    map.on("click", (e: L.LeafletMouseEvent) => {
      onPickRef.current?.(e.latlng.lat, e.latlng.lng);
    });
    mapRef.current = map;
    return () => {
      map.remove();
      mapRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const map = mapRef.current;
    const layer = layerRef.current;
    if (!map || !layer) return;
    layer.clearLayers();
    const bounds: L.LatLngExpression[] = [];

    for (const p of points) {
      L.circleMarker([p.lat, p.lon], {
        radius: 7,
        color: "#0f172a",
        weight: 2,
        fillColor: "#facc15",
        fillOpacity: 0.9,
      })
        .bindTooltip(p.label ?? "Image")
        .addTo(layer);
      bounds.push([p.lat, p.lon]);
    }

    for (const f of features) {
      try {
        const geoLayer = L.geoJSON(f.geometry as GeoJSON.GeoJsonObject, {
          style: STYLE[f.kind],
        });
        if (f.label) geoLayer.bindTooltip(f.label);
        geoLayer.addTo(layer);
        const b = geoLayer.getBounds();
        if (b.isValid()) bounds.push(b.getSouthWest(), b.getNorthEast());
      } catch {
        // A malformed geometry for one feature should not blank the whole map.
      }
    }

    if (pickedPoint) {
      L.circleMarker([pickedPoint.lat, pickedPoint.lon], {
        radius: 8,
        color: "#7c3aed",
        weight: 3,
        fillColor: "#a78bfa",
        fillOpacity: 0.9,
      })
        .bindTooltip("Selected point (approximate)")
        .addTo(layer);
      bounds.push([pickedPoint.lat, pickedPoint.lon]);
    }

    if (bounds.length > 0) {
      map.fitBounds(L.latLngBounds(bounds), { padding: [24, 24], maxZoom: 17 });
    }
  }, [points, features, pickedPoint]);

  return (
    <div>
      <div ref={containerRef} style={{ height, borderRadius: 8, overflow: "hidden" }} />
      {legend && (
        <div className="map-legend">
          {LEGEND.map(([kind, label]) => (
            <span key={kind}>
              <i style={{ background: STYLE[kind].color as string }} />
              {label}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}
