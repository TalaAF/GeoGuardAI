import "leaflet/dist/leaflet.css";
import "@geoman-io/leaflet-geoman-free/dist/leaflet-geoman.css";
import L from "leaflet";
import "@geoman-io/leaflet-geoman-free";
import {
  AlertTriangle,
  CheckCircle2,
  Edit3,
  Eraser,
  Maximize2,
  PenLine,
  RotateCcw,
  Upload,
  X,
} from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import {
  checkWithin,
  formatArea,
  MAX_IMPORT_FILE_BYTES,
  normalizeGeometryInput,
  RAMALLAH_CENTER,
  RAMALLAH_ZOOM,
  totalAreaM2,
  type NormalizedMultiPolygon,
} from "../lib/geometry";

type Mode = "draw" | "import";

const MAIN_STYLE: L.PathOptions = { color: "#2563eb", weight: 2, fillOpacity: 0.15 };
const PARENT_STYLE: L.PathOptions = { color: "#64748b", weight: 2, dashArray: "6 4", fillOpacity: 0.03 };
const PREVIEW_STYLE: L.PathOptions = { color: "#7c3aed", weight: 2, dashArray: "3 3", fillOpacity: 0.1 };

/**
 * Reusable draw/import boundary editor for Zones, Blocks, Parcels, Roads, and a Flight's planned
 * area. This is the only way municipal users create or edit these boundaries — there is no raw-JSON
 * mode; the component builds the underlying geographic data automatically. Anyone who needs to
 * inspect the underlying data can use Swagger or browser developer tools, never this screen.
 *
 * Coordinates are only ever read/written through Leaflet's own `L.geoJSON()` / `layer.toGeoJSON()`,
 * which convert between Leaflet's `[lat, lon]` and the API's `[lon, lat]` internally — nobody using
 * the form ever sees or handles that distinction.
 *
 * Uncontrolled by design: pass a stable `resetKey` (e.g. the record id being edited, or `"new"`) —
 * changing it remounts the map fresh from `initialGeometry`. Editing an unrelated form field must
 * not change `resetKey`, or the drawn boundary would be lost.
 */
export default function GeometryEditor({
  initialGeometry,
  onChange,
  parentBoundary,
  boundaryLabel = "boundary",
  height = 340,
}: {
  initialGeometry?: unknown;
  onChange: (geometry: NormalizedMultiPolygon | null) => void;
  parentBoundary?: { geometry: unknown; label: string } | null;
  boundaryLabel?: string;
  height?: number;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<L.Map | null>(null);
  const mainGroupRef = useRef<L.FeatureGroup | null>(null);
  const parentGroupRef = useRef<L.FeatureGroup | null>(null);
  const previewGroupRef = useRef<L.FeatureGroup | null>(null);
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  const [mode, setMode] = useState<Mode>("draw");
  const [drawing, setDrawing] = useState(false);
  const [editingVertices, setEditingVertices] = useState(false);
  const [removing, setRemoving] = useState(false);
  const [geometry, setGeometry] = useState<NormalizedMultiPolygon | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [containmentWarning, setContainmentWarning] = useState<string | null>(null);
  const [preview, setPreview] = useState<{ geometry: NormalizedMultiPolygon; areaM2: number } | null>(
    null,
  );
  const [importMessage, setImportMessage] = useState<string | null>(null);
  const [importError, setImportError] = useState<string | null>(null);

  const partCount = geometry?.coordinates.length ?? 0;
  const areaM2 = useMemo(() => (geometry ? totalAreaM2(geometry) : 0), [geometry]);

  // --- map setup (once per mount; parent forces a fresh mount via `key={resetKey}`) ---
  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;
    const map = L.map(containerRef.current, { attributionControl: false }).setView(
      RAMALLAH_CENTER,
      RAMALLAH_ZOOM,
    );
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", { maxZoom: 19 }).addTo(map);
    mapRef.current = map;
    mainGroupRef.current = L.featureGroup().addTo(map);
    parentGroupRef.current = L.featureGroup().addTo(map);
    previewGroupRef.current = L.featureGroup().addTo(map);

    const recompute = () => {
      const layers = mainGroupRef.current!.getLayers();
      if (layers.length === 0) {
        setGeometry(null);
        setError(null);
        setContainmentWarning(null);
        return;
      }
      const result = normalizeGeometryInput(mainGroupRef.current!.toGeoJSON());
      if (!result.ok) {
        setGeometry(null);
        setError(result.error);
        setContainmentWarning(null);
        return;
      }
      setError(null);
      setGeometry(result.geometry);
    };
    map.on(
      "pm:create pm:remove pm:edit pm:update pm:vertexadded pm:vertexremoved pm:dragend pm:cut",
      recompute,
    );

    // Load the existing/saved boundary, if any.
    if (initialGeometry) {
      const loaded = normalizeGeometryInput(initialGeometry);
      if (loaded.ok) {
        const layer = L.geoJSON(loaded.geometry as GeoJSON.GeoJsonObject, { style: MAIN_STYLE });
        layer.eachLayer((l) => mainGroupRef.current!.addLayer(l));
        setGeometry(loaded.geometry);
        const bounds = mainGroupRef.current!.getBounds();
        if (bounds.isValid()) map.fitBounds(bounds, { padding: [24, 24], maxZoom: 18 });
      }
    }

    return () => {
      map.off();
      map.remove();
      mapRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // --- keep the parent (reference) boundary layer in sync without disturbing the editable geometry ---
  useEffect(() => {
    const group = parentGroupRef.current;
    if (!group) return;
    group.clearLayers();
    if (!parentBoundary?.geometry) return;
    try {
      const layer = L.geoJSON(parentBoundary.geometry as GeoJSON.GeoJsonObject, {
        style: PARENT_STYLE,
        pmIgnore: true,
      } as L.GeoJSONOptions);
      layer.eachLayer((l) => group.addLayer(l));
      const bounds = group.getBounds();
      // Only auto-fit to the parent when there is no editable geometry of our own yet (first pick).
      if (bounds.isValid() && mainGroupRef.current?.getLayers().length === 0) {
        mapRef.current?.fitBounds(bounds, { padding: [24, 24], maxZoom: 17 });
      }
    } catch {
      // A malformed parent geometry is not this editor's problem to fix — just skip showing it.
    }
  }, [parentBoundary]);

  // --- containment check against the parent, whenever our geometry or the parent changes ---
  useEffect(() => {
    if (!geometry || !parentBoundary?.geometry) {
      setContainmentWarning(null);
      return;
    }
    const result = checkWithin(geometry, parentBoundary.geometry);
    setContainmentWarning(
      result.checked && !result.within
        ? `The ${boundaryLabel} must remain inside the selected ${parentBoundary.label}.`
        : null,
    );
  }, [geometry, parentBoundary, boundaryLabel]);

  // --- report the effective value upward: null whenever it isn't safe to submit yet ---
  useEffect(() => {
    onChangeRef.current(error || containmentWarning ? null : geometry);
  }, [geometry, error, containmentWarning]);

  const setMainGeometry = (next: NormalizedMultiPolygon) => {
    mainGroupRef.current!.clearLayers();
    const layer = L.geoJSON(next as GeoJSON.GeoJsonObject, { style: MAIN_STYLE });
    layer.eachLayer((l) => mainGroupRef.current!.addLayer(l));
    setGeometry(next);
    setError(null);
    const bounds = mainGroupRef.current!.getBounds();
    if (bounds.isValid()) mapRef.current?.fitBounds(bounds, { padding: [24, 24], maxZoom: 18 });
  };

  const startDraw = () => {
    setRemoving(false);
    setEditingVertices(false);
    mapRef.current?.pm.disableGlobalRemovalMode();
    mainGroupRef.current?.pm.disable();
    const next = !drawing;
    setDrawing(next);
    if (next) {
      mapRef.current?.pm.enableDraw("Polygon", {
        snappable: true,
        allowSelfIntersection: false,
        templineStyle: MAIN_STYLE,
        hintlineStyle: { color: "#2563eb", dashArray: "4 4" },
        pathOptions: MAIN_STYLE,
      });
    } else {
      mapRef.current?.pm.disableDraw("Polygon");
    }
  };
  const toggleEdit = () => {
    mapRef.current?.pm.disableDraw("Polygon");
    setDrawing(false);
    mapRef.current?.pm.disableGlobalRemovalMode();
    setRemoving(false);
    const next = !editingVertices;
    setEditingVertices(next);
    if (next) mainGroupRef.current?.pm.enable({ allowSelfIntersection: false });
    else mainGroupRef.current?.pm.disable();
  };
  const toggleRemoveShape = () => {
    mapRef.current?.pm.disableDraw("Polygon");
    setDrawing(false);
    mainGroupRef.current?.pm.disable();
    setEditingVertices(false);
    const next = !removing;
    setRemoving(next);
    if (next) mapRef.current?.pm.enableGlobalRemovalMode();
    else mapRef.current?.pm.disableGlobalRemovalMode();
  };
  const clearAll = () => {
    if (mainGroupRef.current!.getLayers().length > 0 && !window.confirm(`Clear the current ${boundaryLabel}?`)) {
      return;
    }
    mapRef.current?.pm.disableDraw("Polygon");
    mapRef.current?.pm.disableGlobalRemovalMode();
    mainGroupRef.current?.pm.disable();
    setDrawing(false);
    setEditingVertices(false);
    setRemoving(false);
    mainGroupRef.current!.clearLayers();
    setGeometry(null);
    setError(null);
  };
  const zoomToGeometry = () => {
    const group = mainGroupRef.current!.getLayers().length ? mainGroupRef.current! : parentGroupRef.current;
    const bounds = group?.getBounds();
    if (bounds?.isValid()) mapRef.current?.fitBounds(bounds, { padding: [24, 24], maxZoom: 18 });
  };

  const applyPreview = () => {
    if (!preview) return;
    setMainGeometry(preview.geometry);
    setPreview(null);
    previewGroupRef.current?.clearLayers();
  };
  const discardPreview = () => {
    setPreview(null);
    previewGroupRef.current?.clearLayers();
    setImportMessage(null);
    setImportError(null);
  };

  const onImportFile = async (file: File | undefined) => {
    setImportError(null);
    setImportMessage(null);
    discardPreview();
    if (!file) return;
    if (file.size > MAX_IMPORT_FILE_BYTES) {
      setImportError(`That file is too large (max ${Math.round(MAX_IMPORT_FILE_BYTES / 1024)} KB).`);
      return;
    }
    const text = await file.text();
    const result = normalizeGeometryInput(text);
    if (!result.ok) {
      setImportError("Invalid boundary file. " + result.error);
      return;
    }
    previewGroupRef.current?.clearLayers();
    const layer = L.geoJSON(result.geometry as GeoJSON.GeoJsonObject, { style: PREVIEW_STYLE });
    layer.eachLayer((l) => previewGroupRef.current!.addLayer(l));
    const bounds = previewGroupRef.current?.getBounds();
    if (bounds?.isValid()) mapRef.current?.fitBounds(bounds, { padding: [24, 24], maxZoom: 18 });
    setPreview({ geometry: result.geometry, areaM2: result.areaM2 });
    setImportMessage(
      result.partCount === 1 ? "1 boundary imported" : `${result.partCount} boundary parts imported`,
    );
  };

  return (
    <div className="geometry-editor">
      <div className="geometry-editor-tabs">
        <button type="button" className={mode === "draw" ? "active" : ""} onClick={() => setMode("draw")}>
          <PenLine size={13} /> Draw on map
        </button>
        <button type="button" className={mode === "import" ? "active" : ""} onClick={() => setMode("import")}>
          <Upload size={13} /> Import boundary file
        </button>
      </div>

      <div ref={containerRef} className="geometry-editor-map" style={{ height }} />

      <div className="map-legend">
        <span><i style={{ background: MAIN_STYLE.color as string }} /> This {boundaryLabel}</span>
        {parentBoundary && (
          <span><i style={{ background: PARENT_STYLE.color as string }} /> {parentBoundary.label} (reference)</span>
        )}
        {preview && <span><i style={{ background: PREVIEW_STYLE.color as string }} /> Imported (not saved yet)</span>}
      </div>

      {mode === "draw" && (
        <div className="geometry-editor-toolbar">
          <button type="button" className={drawing ? "primary" : "secondary"} onClick={startDraw}>
            <PenLine size={13} /> {drawing ? "Stop drawing" : "Draw boundary"}
          </button>
          <button
            type="button"
            className={editingVertices ? "primary" : "secondary"}
            onClick={toggleEdit}
            disabled={partCount === 0}
          >
            <Edit3 size={13} /> {editingVertices ? "Stop editing" : "Edit boundary"}
          </button>
          <button
            type="button"
            className={removing ? "primary" : "secondary"}
            onClick={toggleRemoveShape}
            disabled={partCount === 0}
          >
            <Eraser size={13} /> {removing ? "Click a part to delete…" : "Delete boundary"}
          </button>
          <button type="button" className="secondary" onClick={clearAll} disabled={partCount === 0}>
            <RotateCcw size={13} /> Clear
          </button>
          <button type="button" className="secondary" onClick={zoomToGeometry}>
            <Maximize2 size={13} /> Zoom to boundary
          </button>
        </div>
      )}

      {mode === "import" && (
        <div className="geometry-editor-toolbar">
          <label className="secondary" style={{ cursor: "pointer" }}>
            <Upload size={13} /> Import boundary file (.geojson / .json)
            <input
              type="file"
              accept=".geojson,.json,application/geo+json,application/json"
              style={{ display: "none" }}
              onChange={(e) => void onImportFile(e.target.files?.[0])}
            />
          </label>
          {preview && (
            <>
              <button type="button" className="primary" onClick={applyPreview}>
                <CheckCircle2 size={13} /> Use this geometry
              </button>
              <button type="button" className="secondary" onClick={discardPreview}>
                <X size={13} /> Discard
              </button>
            </>
          )}
        </div>
      )}

      <p className="geometry-editor-hint">
        Click points on the map to draw the {boundaryLabel}. Click the first point again to finish —
        the system creates the underlying map data automatically. Drawn boundaries represent
        municipality-supplied data and should be verified before operational use.
      </p>

      <div className="geometry-editor-status">
        {geometry && !error && !containmentWarning && (
          <span className="geometry-editor-summary">
            {partCount} part{partCount === 1 ? "" : "s"} · approx. {formatArea(areaM2)}
          </span>
        )}
        {!geometry && !error && (
          <span className="geometry-editor-summary muted">No {boundaryLabel} drawn yet.</span>
        )}
        {error && (
          <span className="geometry-editor-error">
            <AlertTriangle size={13} /> {error}
          </span>
        )}
        {containmentWarning && (
          <span className="geometry-editor-error">
            <AlertTriangle size={13} /> {containmentWarning}
          </span>
        )}
        {mode === "import" && importError && (
          <span className="geometry-editor-error">
            <AlertTriangle size={13} /> {importError}
          </span>
        )}
        {mode === "import" && importMessage && (
          <span className="geometry-editor-summary">
            {importMessage} · approx. {formatArea(preview?.areaM2 ?? 0)} — click "Use this geometry" to
            apply it.
          </span>
        )}
      </div>
    </div>
  );
}
