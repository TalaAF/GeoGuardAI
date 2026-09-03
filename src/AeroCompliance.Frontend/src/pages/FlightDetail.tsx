import { ArrowLeft, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { api, errorMessage, page as getPage } from "../api/client";
import type {
  Detection,
  FlightDetails,
  ImageInfo,
  Mission,
  ViolationSummary,
} from "../api/types";
import { hasRole, useAuth } from "../auth/AuthContext";
import {
  Card,
  Empty,
  Notice,
  PageTitle,
  ShortId,
  Spinner,
  Status,
  fmtDate,
  fmtPercent,
} from "../components/Common";
import FlightEdit from "../components/FlightEdit";
import ImageUploadPanel from "../components/ImageUploadPanel";
import GeoMap, { type MapFeature, type MapPoint } from "../components/GeoMap";

export default function FlightDetail() {
  const { id } = useParams();
  const { user } = useAuth();
  const [flight, setFlight] = useState<FlightDetails | null>(null);
  const [missions, setMissions] = useState<Mission[]>([]);
  const [images, setImages] = useState<ImageInfo[]>([]);
  const [detections, setDetections] = useState<Detection[]>([]);
  const [violations, setViolations] = useState<ViolationSummary[]>([]);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [mission, setMission] = useState({
    purpose: "",
    altitudeM: "",
    overlapPercent: "",
    notes: "",
  });
  const load = useCallback(async () => {
    if (!id) return;
    setError("");
    try {
      const [f, m, i, d, v] = await Promise.all([
        api.get<FlightDetails>(`/flights/${id}`),
        getPage<Mission>(`/flights/${id}/missions`, { pageSize: 100 }),
        getPage<ImageInfo>(`/flights/${id}/images`, { pageSize: 100 }),
        getPage<Detection>("/detections", { flightId: id, pageSize: 100 }),
        getPage<ViolationSummary>("/violations", { flightId: id, pageSize: 100 }),
      ]);
      setFlight(f.data);
      setMissions(m.items);
      setImages(i.items);
      setDetections(d.items);
      setViolations(v.items);
    } catch (e) {
      setError(errorMessage(e));
    }
  }, [id]);
  useEffect(() => {
    void load();
  }, [load]);
  const addMission = async (e: FormEvent) => {
    e.preventDefault();
    try {
      await api.post(`/flights/${id}/missions`, {
        purpose: mission.purpose || null,
        altitudeM: mission.altitudeM ? Number(mission.altitudeM) : null,
        overlapPercent: mission.overlapPercent
          ? Number(mission.overlapPercent)
          : null,
        notes: mission.notes || null,
      });
      setMission({ purpose: "", altitudeM: "", overlapPercent: "", notes: "" });
      await load();
      setMessage("Mission added.");
    } catch (e) {
      setError(errorMessage(e));
    }
  };
  const transition = async (targetStatus: string) => {
    try {
      await api.post(`/flights/${id}/status-transitions`, {
        targetStatus,
        reason: "Updated from the AeroCompliance frontend.",
      });
      await load();
    } catch (e) {
      setError(errorMessage(e));
    }
  };
  if (!flight && !error) return <Spinner />;
  const canManage = hasRole(user, [
    "Admin",
    "MunicipalityManager",
    "DroneOperator",
  ]);
  const transitions: Record<string, string[]> = {
    planned: ["in_progress", "aborted"],
    in_progress: ["completed", "aborted"],
    completed: ["flagged_low_quality"],
  };
  return (
    <div className="page">
      <PageTitle
        title={flight?.missionName || "Flight details"}
        description={id}
        actions={
          <>
            <Link className="secondary" to="/flights">
              <ArrowLeft size={14} />
              Back
            </Link>
            <button className="secondary" onClick={() => void load()}>
              <RefreshCw size={14} />
            </button>
          </>
        }
      />
      {error && <Notice kind="error">{error}</Notice>}
      {message && <Notice kind="success">{message}</Notice>}
      {flight && (
        <>
          <div className="grid stats">
            <Card>
              <div className="detail">
                <small>Status</small>
                <Status value={flight.status} />
              </div>
            </Card>
            <Card>
              <div className="detail">
                <small>Operator</small>
                <strong>
                  {flight.operatorName || (
                    <ShortId value={flight.droneOperatorId} />
                  )}
                </strong>
              </div>
            </Card>
            <Card>
              <div className="detail">
                <small>Images</small>
                <strong>{flight.imageCount}</strong>
              </div>
            </Card>
            <Card>
              <div className="detail">
                <small>Missions</small>
                <strong>{flight.missionCount}</strong>
              </div>
            </Card>
          </div>
          {canManage && transitions[flight.status]?.length > 0 && (
            <Card>
              <h2>Valid status transitions</h2>
              <div className="actions">
                {transitions[flight.status].map((x) => (
                  <button
                    key={x}
                    className="secondary"
                    onClick={() => void transition(x)}
                  >
                    {x.replaceAll("_", " ")}
                  </button>
                ))}
              </div>
            </Card>
          )}
          {canManage && flight.status === "planned" && (
            <div style={{ marginTop: 16 }}>
              <FlightEdit flight={flight} onSaved={load} />
            </div>
          )}
          <div className="grid two-col" style={{ marginTop: 16 }}>
            <div className="grid">
              <Card>
                <h2>Upload images for AI processing</h2>
                <ImageUploadPanel flightId={id!} onProcessed={() => void load()} />
              </Card>
              <Card>
                <h2>Stored images</h2>
                {images.length === 0 ? (
                  <Empty
                    title="No images"
                    detail="Upload an authorized image to begin analysis."
                  />
                ) : (
                  <div className="table-wrap">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>File</th>
                          <th>Image</th>
                          <th>Status</th>
                          <th>Attempts</th>
                          <th>Captured</th>
                          <th>Location</th>
                        </tr>
                      </thead>
                      <tbody>
                        {images.map((x) => (
                          <tr key={x.id}>
                            <td>{x.originalFileName}</td>
                            <td>
                              <ShortId value={x.id} />
                            </td>
                            <td>
                              <Status value={x.processingStatus} />
                              {x.metadataRequiresReview && (
                                <div style={{ marginTop: 4 }}>
                                  <span className="badge-source missing">Needs review</span>
                                </div>
                              )}
                            </td>
                            <td>{x.attemptCount}</td>
                            <td>
                              {fmtDate(x.capturedAt)}
                              <div>
                                <span
                                  className={`badge-source ${x.captureTimeSource.toLowerCase()}`}
                                >
                                  {x.captureTimeSource}
                                </span>
                              </div>
                            </td>
                            <td>
                              {x.latitude != null && x.longitude != null
                                ? `${x.latitude.toFixed(5)}, ${x.longitude.toFixed(5)}`
                                : "—"}
                              <div>
                                <span
                                  className={`badge-source ${x.locationSource.toLowerCase()}`}
                                >
                                  {x.locationSource}
                                </span>
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </Card>
            </div>
            <div className="grid">
              <Card>
                <h2>Flight missions</h2>
                {missions.length === 0 ? (
                  <Empty
                    title="No mission details"
                    detail="Add purpose, altitude, and overlap while the flight is planned."
                  />
                ) : (
                  <div className="table-wrap">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>Purpose</th>
                          <th>Altitude</th>
                          <th>Overlap</th>
                        </tr>
                      </thead>
                      <tbody>
                        {missions.map((x) => (
                          <tr key={x.id}>
                            <td>{x.purpose || "—"}</td>
                            <td>{x.altitudeM ?? "—"}</td>
                            <td>
                              {x.overlapPercent != null
                                ? `${x.overlapPercent}%`
                                : "—"}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
                {canManage && flight.status === "planned" && (
                  <form
                    className="form"
                    onSubmit={addMission}
                    style={{ marginTop: 14 }}
                  >
                    <label>
                      Purpose
                      <input
                        value={mission.purpose}
                        onChange={(e) =>
                          setMission({ ...mission, purpose: e.target.value })
                        }
                      />
                    </label>
                    <div className="field-row">
                      <label>
                        Altitude
                        <input
                          type="number"
                          value={mission.altitudeM}
                          onChange={(e) =>
                            setMission({
                              ...mission,
                              altitudeM: e.target.value,
                            })
                          }
                        />
                      </label>
                      <label>
                        Overlap %
                        <input
                          type="number"
                          value={mission.overlapPercent}
                          onChange={(e) =>
                            setMission({
                              ...mission,
                              overlapPercent: e.target.value,
                            })
                          }
                        />
                      </label>
                    </div>
                    <label>
                      Notes
                      <textarea
                        value={mission.notes}
                        onChange={(e) =>
                          setMission({ ...mission, notes: e.target.value })
                        }
                      />
                    </label>
                    <button className="secondary">Add mission</button>
                  </form>
                )}
              </Card>
              {(images.some((i) => i.latitude != null) ||
                detections.length > 0 ||
                violations.length > 0) && (
                <Card>
                  <h2>Map</h2>
                  <p style={{ fontSize: 12, color: "#64748b", margin: "-6px 0 10px" }}>
                    Blue outlines are raw AI detections — a proposal only. Open a candidate
                    violation below to see it with its reviewed (confirmed/dismissed) styling;
                    a violation is never just a detection that was left unreviewed.
                  </p>
                  <GeoMap
                    points={images
                      .filter((i): i is ImageInfo & { latitude: number; longitude: number } =>
                        i.latitude != null && i.longitude != null,
                      )
                      .map(
                        (i): MapPoint => ({
                          id: i.id,
                          lat: i.latitude,
                          lon: i.longitude,
                          label: i.originalFileName,
                        }),
                      )}
                    features={detections.map(
                      (d): MapFeature => ({
                        id: d.id,
                        geometry: d.geometry,
                        kind: "detection",
                        label: `AI detection · ${d.classLabel}`,
                      }),
                    )}
                    legend
                  />
                </Card>
              )}
              {(detections.length > 0 || violations.length > 0) && (
                <Card>
                  <h2>Processing results</h2>
                  <p>
                    <strong>{detections.length}</strong> AI Detection(s) ·{" "}
                    <strong>{violations.length}</strong> Candidate Violation(s)
                  </p>
                  {detections.map((d) => (
                    <div className="detail" key={d.id}>
                      <small>
                        AI Detection · <ShortId value={d.id} />
                      </small>
                      <strong>
                        {d.classLabel} ·{" "}
                        <span className="confidence">
                          {fmtPercent(d.confidence)}
                        </span>
                      </strong>
                      {d.modelName && (
                        <p>
                          {d.modelName} {d.modelVersion}
                        </p>
                      )}
                    </div>
                  ))}
                  {violations.map((v) => (
                    <p key={v.id}>
                      <Link className="secondary" to={`/violations/${v.id}`}>
                        Open candidate <ShortId value={v.id} />
                      </Link>
                    </p>
                  ))}
                </Card>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
