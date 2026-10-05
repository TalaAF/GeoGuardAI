import { useEffect, useState } from "react";
import { api, errorMessage } from "../api/client";
import { Card, Notice, Spinner, fmtDate } from "./Common";

interface GisReferenceSummary {
  referenceType: string;
  label: string;
  description: string;
  featureCount: number;
  averageAreaM2: number | null;
  sourceLayer: string;
  sourceCrs: string;
  lastImportedAt: string | null;
}

/**
 * Imported GIS layers, separated into operational data and reference-only data.
 *
 * The distinction matters: operational parcels drive compliance decisions, whereas the imported
 * reference layers are historical or comparative material. Presenting them identically would imply
 * the reference layers carry the same authority, which they do not.
 */
const REFERENCE_ONLY = new Set([
  "BuildingGroundTruth",
  "ParcelEncroachmentReference",
  "RoadEncroachmentReference",
]);

export default function GisReferencePanel() {
  const [rows, setRows] = useState<GisReferenceSummary[] | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;
    api
      .get<GisReferenceSummary[]>("/gis-reference/summary")
      .then(({ data }) => !cancelled && setRows(data))
      .catch((e) => !cancelled && setError(errorMessage(e)));
    return () => {
      cancelled = true;
    };
  }, []);

  if (error) return <Notice kind="error">{error}</Notice>;
  if (!rows) return <Spinner />;
  if (rows.length === 0) {
    return (
      <Card>
        <h2>Imported reference data</h2>
        <Notice kind="info">No reference GIS layers have been imported.</Notice>
      </Card>
    );
  }

  const hasRoadReference = rows.some((r) => r.referenceType === "RoadEncroachmentReference");

  return (
    <Card>
      <h2>Imported reference data</h2>
      <Notice kind="info">
        Parcels, zones, blocks and roads managed on this page are{" "}
        <strong>operational data</strong> and drive compliance decisions. The layers below are{" "}
        <strong>reference-only</strong>: they are used for comparison and context, never as the
        authority for a violation.
      </Notice>
      {hasRoadReference && (
        <Notice kind="error">
          Road-encroachment areas are polygons from a previous analysis, not a road network. An
          overlap is recorded as a <code>ReferenceRoadEncroachmentMatch</code> — a match against
          known reference areas, not a complete road-network check.
        </Notice>
      )}
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Layer</th>
              <th>Classification</th>
              <th>Source layer</th>
              <th>CRS</th>
              <th>Features</th>
              <th>Imported</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.referenceType}>
                <td className="cell-wrap">
                  <strong>{r.label}</strong>
                  <div className="muted">{r.description}</div>
                </td>
                <td>
                  <span
                    className={`source-badge ${
                      REFERENCE_ONLY.has(r.referenceType) ? "source-reference" : "source-real"
                    }`}
                  >
                    {REFERENCE_ONLY.has(r.referenceType) ? "Reference only" : "Operational"}
                  </span>
                </td>
                <td className="cell-wrap">
                  <code>{r.sourceLayer}</code>
                </td>
                <td>{r.sourceCrs}</td>
                <td>{r.featureCount.toLocaleString()}</td>
                <td>{fmtDate(r.lastImportedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}
