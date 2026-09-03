import { RefreshCw } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { api, errorMessage, page as getPage } from "../api/client";
import type { Detection, Paged } from "../api/types";
import {
  Card,
  Empty,
  Notice,
  PageTitle,
  Pager,
  Spinner,
  fmtDate,
  fmtPercent,
} from "../components/Common";
import GeoMap from "../components/GeoMap";
import { shortRef } from "../lib/labels";

export default function Detections() {
  const [data, setData] = useState<Paged<Detection> | null>(null);
  const [selected, setSelected] = useState<Detection | null>(null);
  const [p, setP] = useState(1);
  const [className, setClass] = useState("");
  const [confidence, setConfidence] = useState("");
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    try {
      setData(
        await getPage<Detection>("/detections", {
          page: p,
          pageSize: 20,
          className: className || undefined,
          minConfidence: confidence || undefined,
        }),
      );
    } catch (e) {
      setError(errorMessage(e));
    }
  }, [p, className, confidence]);
  useEffect(() => {
    void load();
  }, [load]);
  const details = async (id: string) => {
    try {
      setSelected((await api.get<Detection>(`/detections/${id}`)).data);
    } catch (e) {
      setError(errorMessage(e));
    }
  };
  return (
    <div className="page">
      <PageTitle
        title="AI Detections"
        description="Model observations are evidence inputs, not confirmed violations."
        actions={
          <button className="secondary" onClick={() => void load()}>
            <RefreshCw size={14} />
            Refresh
          </button>
        }
      />
      {error && <Notice kind="error">{error}</Notice>}
      <div className="grid two-col">
        <Card>
          <div className="toolbar">
            <input
              placeholder="Class name"
              value={className}
              onChange={(e) => {
                setP(1);
                setClass(e.target.value);
              }}
            />
            <input
              type="number"
              min="0"
              max="1"
              step=".05"
              placeholder="Minimum confidence"
              value={confidence}
              onChange={(e) => {
                setP(1);
                setConfidence(e.target.value);
              }}
            />
          </div>
          {!data ? (
            <Spinner />
          ) : data.items.length === 0 ? (
            <Empty
              title="No AI detections"
              detail="Completed image analysis results will appear here."
            />
          ) : (
            <>
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Source image</th>
                      <th>Flight</th>
                      <th>Class</th>
                      <th>Confidence</th>
                      <th>Created</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.items.map((x) => (
                      <tr
                        key={x.id}
                        onClick={() => void details(x.id)}
                        style={{ cursor: "pointer" }}
                      >
                        <td>{x.sourceImageFileName || "Not available"}</td>
                        <td>{x.flightMissionName || "Untitled flight"}</td>
                        <td>{x.classLabel || "Unclassified"}</td>
                        <td className="confidence">
                          {fmtPercent(x.confidence)}
                        </td>
                        <td>{fmtDate(x.createdAt)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Pager
                page={data.page}
                totalPages={data.totalPages}
                onChange={setP}
              />
            </>
          )}
        </Card>
        <Card>
          <h2>Detection details</h2>
          {!selected ? (
            <Empty
              title="Select a detection"
              detail="Choose a row to see its boundary on the map and its compliance status."
            />
          ) : (
            <div className="form">
              <Notice>
                AI detection — not yet a confirmed violation. A human reviewer must confirm or
                dismiss any resulting candidate case.
              </Notice>
              <div className="detail-grid">
                <div className="detail">
                  <small>Classification</small>
                  <strong>{selected.classLabel || "Unclassified"}</strong>
                </div>
                <div className="detail">
                  <small>Confidence</small>
                  <strong>{fmtPercent(selected.confidence)}</strong>
                </div>
                <div className="detail">
                  <small>Source image</small>
                  <strong>{selected.sourceImageFileName || "Not available"}</strong>
                </div>
                <div className="detail">
                  <small>Flight</small>
                  <strong>{selected.flightMissionName || "Untitled flight"}</strong>
                </div>
                <div className="detail">
                  <small>Detected on</small>
                  <strong>{fmtDate(selected.createdAt)}</strong>
                </div>
                <div className="detail">
                  <small>Reference</small>
                  <strong>Detection {shortRef(selected.id)}</strong>
                </div>
              </div>
              {selected.geometry ? (
                <GeoMap
                  features={[
                    { id: selected.id, geometry: selected.geometry, kind: "detection", label: "AI detection" },
                  ]}
                  legend
                />
              ) : (
                <Notice kind="info">No geographic boundary is available for this detection.</Notice>
              )}
              {(selected.modelName || selected.modelVersion) && (
                <details className="technical-info">
                  <summary>Technical information</summary>
                  <div className="detail-grid">
                    <div className="detail">
                      <small>Model</small>
                      <strong>{selected.modelName || "Not returned"}</strong>
                    </div>
                    <div className="detail">
                      <small>Model version</small>
                      <strong>{selected.modelVersion || "Not returned"}</strong>
                    </div>
                  </div>
                </details>
              )}
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}
