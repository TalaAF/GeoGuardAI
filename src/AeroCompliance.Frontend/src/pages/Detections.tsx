import { RefreshCw } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { api, errorMessage, page as getPage } from "../api/client";
import type {
  ComplianceEvaluation,
  Detection,
  Paged,
  ReEvaluationResult,
} from "../api/types";
import {
  Card,
  Empty,
  Notice,
  PageTitle,
  Pager,
  SourceBadge,
  Spinner,
  fmtDate,
  fmtPercent,
} from "../components/Common";
import GeoMap from "../components/GeoMap";
import ComplianceResults from "../components/ComplianceResults";
import { useAuth } from "../auth/AuthContext";
import { canReevaluateCompliance } from "../auth/capabilities";
import { shortRef } from "../lib/labels";
import {
  APPROXIMATE_WARNING,
  MOCK_WARNING,
  aiClassLabel,
  provenanceBadge,
} from "../lib/provenance";
import type { FlightSummary } from "../api/types";

export default function Detections() {
  const [data, setData] = useState<Paged<Detection> | null>(null);
  const [selected, setSelected] = useState<Detection | null>(null);
  const [p, setP] = useState(1);
  const [className, setClass] = useState("");
  const [confidence, setConfidence] = useState("");
  const [resultSource, setResultSource] = useState("");
  const [mode, setMode] = useState("");
  const [flightId, setFlightId] = useState("");
  const [approxOnly, setApproxOnly] = useState(false);
  const [reviewOnly, setReviewOnly] = useState(false);
  const [classes, setClasses] = useState<string[]>([]);
  const [flights, setFlights] = useState<FlightSummary[]>([]);
  const [error, setError] = useState("");
  const { user } = useAuth();
  // Mirrors the backend ManageGis policy that guards the re-evaluation endpoint.
  const canReevaluate = canReevaluateCompliance(user);
  const [evaluations, setEvaluations] = useState<ComplianceEvaluation[]>([]);
  const [evalLoading, setEvalLoading] = useState(false);
  const [evalBusy, setEvalBusy] = useState(false);
  const [evalMessage, setEvalMessage] = useState("");

  // Compliance results are fetched per selected detection rather than with the list, so opening
  // the page stays cheap when nothing is selected.
  useEffect(() => {
    if (!selected) {
      setEvaluations([]);
      setEvalMessage("");
      return;
    }
    let cancelled = false;
    setEvalLoading(true);
    setEvalMessage("");
    api
      .get<ComplianceEvaluation[]>(`/detections/${selected.id}/compliance-evaluations`)
      .then(({ data }) => {
        if (!cancelled) setEvaluations(data);
      })
      .catch(() => {
        if (!cancelled) setEvaluations([]);
      })
      .finally(() => {
        if (!cancelled) setEvalLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [selected]);

  const reevaluate = async () => {
    if (!selected || evalBusy) return; // guards a double click
    setEvalBusy(true);
    setError("");
    try {
      const { data } = await api.post<ReEvaluationResult>(
        `/detections/${selected.id}/evaluate-compliance`,
      );
      setEvaluations(data.evaluations);
      setEvalMessage(data.summary);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setEvalBusy(false);
    }
  };
  const load = useCallback(async () => {
    try {
      setData(
        await getPage<Detection>("/detections", {
          page: p,
          pageSize: 20,
          className: className || undefined,
          minConfidence: confidence || undefined,
          resultSource: resultSource || undefined,
          mode: mode || undefined,
          flightId: flightId || undefined,
          // Only send the boolean filters when switched on, so "off" means "no filter"
          // rather than "must be false".
          isApproximate: approxOnly ? true : undefined,
          requiresManualReview: reviewOnly ? true : undefined,
        }),
      );
    } catch (e) {
      setError(errorMessage(e));
    }
  }, [p, className, confidence, resultSource, mode, flightId, approxOnly, reviewOnly]);

  // Filter option sources are loaded once; failures leave the dropdowns empty rather than
  // blocking the page, since the list itself still works without them.
  useEffect(() => {
    api
      .get<string[]>("/detections/classes")
      .then((r) => setClasses(r.data))
      .catch(() => setClasses([]));
    getPage<FlightSummary>("/flights", { page: 1, pageSize: 100 })
      .then((r) => setFlights(r.items))
      .catch(() => setFlights([]));
  }, []);
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
          <div className="filter-bar">
            <label>
              AI class
              <select
                value={className}
                onChange={(e) => {
                  setP(1);
                  setClass(e.target.value);
                }}
              >
                <option value="">All classes</option>
                {classes.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Min. confidence
              <input
                type="number"
                min="0"
                max="1"
                step=".05"
                placeholder="0.00"
                value={confidence}
                onChange={(e) => {
                  setP(1);
                  setConfidence(e.target.value);
                }}
              />
            </label>
            <label>
              Source
              <select
                value={resultSource}
                onChange={(e) => {
                  setP(1);
                  setResultSource(e.target.value);
                }}
              >
                <option value="">All sources</option>
                <option value="RealYoloEstimatedGeometry">Real YOLO — Estimated</option>
                <option value="RealYoloGeoreferencedGeometry">Real YOLO — Validated</option>
                <option value="RealYoloModelOnly">Real YOLO — Model only</option>
                <option value="MockGeographicScenario">Mock scenario</option>
                <option value="ImportedGisReference">Imported GIS reference</option>
              </select>
            </label>
            <label>
              Mode
              <select
                value={mode}
                onChange={(e) => {
                  setP(1);
                  setMode(e.target.value);
                }}
              >
                <option value="">All modes</option>
                <option value="yolo">Real (yolo)</option>
                <option value="mock">Mock</option>
              </select>
            </label>
            <label>
              Flight
              <select
                value={flightId}
                onChange={(e) => {
                  setP(1);
                  setFlightId(e.target.value);
                }}
              >
                <option value="">All flights</option>
                {flights.map((f) => (
                  <option key={f.id} value={f.id}>
                    {f.missionName}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Approximate only
              <input
                type="checkbox"
                checked={approxOnly}
                onChange={(e) => {
                  setP(1);
                  setApproxOnly(e.target.checked);
                }}
              />
            </label>
            <label>
              Needs review
              <input
                type="checkbox"
                checked={reviewOnly}
                onChange={(e) => {
                  setP(1);
                  setReviewOnly(e.target.checked);
                }}
              />
            </label>
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
                      <th>AI class</th>
                      <th>Confidence</th>
                      <th>Source</th>
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
                        <td className="cell-wrap">
                          {x.sourceImageFileName || "Not available"}
                        </td>
                        <td className="cell-wrap">
                          {x.flightMissionName || "Untitled flight"}
                        </td>
                        <td>{aiClassLabel(x.className, x.classId)}</td>
                        <td className="confidence">
                          {fmtPercent(x.confidence)}
                        </td>
                        <td>
                          <div className="badge-stack">
                            <SourceBadge
                              resultSource={x.resultSource}
                              mode={x.analysisMode}
                              fileName={x.sourceImageFileName}
                            />
                            {x.requiresManualReview && (
                              <span className="status status-under_review">Needs review</span>
                            )}
                          </div>
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
              {/* Provenance-specific warning. The generic notice above is never the only
                  distinction between an estimate, synthetic data and a validated result. */}
              {selected.isApproximate && (
                <Notice kind="error">
                  <strong>Estimated geographic location.</strong> {APPROXIMATE_WARNING}
                </Notice>
              )}
              {selected.resultSource === "MockGeographicScenario" && (
                <Notice kind="info">
                  <strong>Deterministic development scenario.</strong> {MOCK_WARNING}
                </Notice>
              )}
              <div className="badge-stack">
                <SourceBadge
                  resultSource={selected.resultSource}
                  mode={selected.analysisMode}
                  fileName={selected.sourceImageFileName}
                />
                {selected.requiresManualReview && (
                  <span className="status status-under_review">Requires manual review</span>
                )}
                {provenanceBadge(selected.resultSource, { mode: selected.analysisMode }).legacy && (
                  <span className="status">Legacy row — source inferred</span>
                )}
              </div>
              <div className="detail-grid">
                <div className="detail">
                  <small>AI class</small>
                  <strong>{aiClassLabel(selected.className, selected.classId)}</strong>
                </div>
                <div className="detail">
                  <small>Confidence</small>
                  <strong>{fmtPercent(selected.confidence)}</strong>
                </div>
                <div className="detail">
                  <small>Model</small>
                  <strong>
                    {selected.modelName || "Not reported"}
                    {selected.modelVersion ? ` · ${selected.modelVersion}` : ""}
                  </strong>
                </div>
                <div className="detail">
                  <small>Analysis mode</small>
                  <strong>{selected.analysisMode || "Not reported"}</strong>
                </div>
                <div className="detail">
                  <small>Source image</small>
                  <strong className="cell-wrap">
                    {selected.sourceImageFileName || "Not available"}
                  </strong>
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
                <div className="detail">
                  <small>Analysis request</small>
                  <strong>
                    {selected.analysisRequestId
                      ? shortRef(selected.analysisRequestId)
                      : "Not reported"}
                  </strong>
                </div>
                {selected.isApproximate && (
                  <>
                    <div className="detail">
                      <small>Georeferencing</small>
                      <strong>
                        {selected.georeferencingStatus || "Estimated"} ·{" "}
                        {selected.georeferencingConfidence || "Low"} confidence
                      </strong>
                    </div>
                    <div className="detail">
                      <small>Method</small>
                      <strong className="cell-wrap">
                        {selected.sourceGeoreferencingMethod || "Not reported"}
                      </strong>
                    </div>
                    <div className="detail">
                      <small>Trusted ROI radius</small>
                      <strong>
                        {selected.trustedRoiRadiusM != null
                          ? `${selected.trustedRoiRadiusM} m`
                          : "Not reported"}
                      </strong>
                    </div>
                    <div className="detail">
                      <small>Distance from anchor</small>
                      <strong>
                        {selected.distanceFromAnchorM != null
                          ? `${selected.distanceFromAnchorM.toFixed(2)} m`
                          : "Not reported"}
                      </strong>
                    </div>
                  </>
                )}
              </div>
              {selected.geometry ? (
                <>
                  {selected.isApproximate && (
                    <Notice kind="error">
                      Approximate geometry — the outline below is positioned by an estimated
                      georeference and is experimental only.
                    </Notice>
                  )}
                  <GeoMap
                    features={[
                      {
                        id: selected.id,
                        geometry: selected.geometry,
                        // Styling follows recorded provenance so an estimate never looks validated.
                        kind: selected.isApproximate
                          ? "detectionEstimated"
                          : selected.resultSource === "MockGeographicScenario"
                            ? "detectionMock"
                            : "detection",
                        label: provenanceBadge(selected.resultSource, {
                          mode: selected.analysisMode,
                        }).label,
                      },
                    ]}
                    legend
                  />
                </>
              ) : (
                <Notice kind="info">
                  No geographic boundary is available for this detection, so nothing is drawn on the
                  map. No position is inferred.
                </Notice>
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
              <ComplianceResults
                items={evaluations}
                loading={evalLoading}
                canReevaluate={canReevaluate}
                onReevaluate={() => void reevaluate()}
                busy={evalBusy}
                message={evalMessage}
              />
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}
