import { ArrowLeft, RefreshCw, RotateCcw, Upload } from "lucide-react";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { Link, useParams } from "react-router-dom";
import { api, errorMessage, page as getPage } from "../api/client";
import type {
  Detection,
  FlightDetails,
  ImageInfo,
  ImageReceipt,
  Mission,
  Paged,
  ProcessingStatus,
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

const terminal = new Set([
  "Completed",
  "AnalysisFailed",
  "ComplianceFailed",
  "UploadFailed",
]);
export default function FlightDetail() {
  const { id } = useParams();
  const { user } = useAuth();
  const [flight, setFlight] = useState<FlightDetails | null>(null);
  const [missions, setMissions] = useState<Mission[]>([]);
  const [images, setImages] = useState<ImageInfo[]>([]);
  const [status, setStatus] = useState<ProcessingStatus | null>(null);
  const [receipt, setReceipt] = useState<ImageReceipt | null>(null);
  const [detections, setDetections] = useState<Detection[]>([]);
  const [violations, setViolations] = useState<ViolationSummary[]>([]);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [mission, setMission] = useState({
    purpose: "",
    altitudeM: "",
    overlapPercent: "",
    notes: "",
  });
  const [form, setForm] = useState({
    capturedAt: new Date().toISOString().slice(0, 16),
    latitude: "",
    longitude: "",
    altitudeM: "",
    cameraHeadingDeg: "",
    gpsAccuracyCm: "",
    mockScenario: "none",
  });
  const load = useCallback(async () => {
    if (!id) return;
    setError("");
    try {
      const [f, m, i] = await Promise.all([
        api.get<FlightDetails>(`/flights/${id}`),
        getPage<Mission>(`/flights/${id}/missions`, { pageSize: 100 }),
        getPage<ImageInfo>(`/flights/${id}/images`, { pageSize: 100 }),
      ]);
      setFlight(f.data);
      setMissions(m.items);
      setImages(i.items);
    } catch (e) {
      setError(errorMessage(e));
    }
  }, [id]);
  useEffect(() => {
    void load();
  }, [load]);
  useEffect(() => {
    if (!receipt) return;
    let cancelled = false;
    let timer: number;
    const poll = async () => {
      try {
        const next = (
          await api.get<ProcessingStatus>(
            `/images/${receipt.imageId}/processing-status`,
          )
        ).data;
        if (cancelled) return;
        setStatus(next);
        if (terminal.has(next.status)) {
          await load();
          if (next.status === "Completed" && id) {
            const [d, v] = await Promise.all([
              getPage<Detection>("/detections", {
                imageId: receipt.imageId,
                pageSize: 100,
              }),
              getPage<ViolationSummary>("/violations", {
                flightId: id,
                pageSize: 100,
              }),
            ]);
            if (!cancelled) {
              setDetections(d.items);
              setViolations(v.items);
            }
          }
          return;
        }
      } catch (e) {
        if (!cancelled) setError(errorMessage(e));
        return;
      }
      timer = window.setTimeout(poll, 1500);
    };
    void poll();
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [receipt, id, load]);
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
  const upload = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) {
      setError("Select an image file.");
      return;
    }
    if (file.size > 250 * 1024 * 1024) {
      setError("The selected file exceeds 250 MB.");
      return;
    }
    if (
      (form.latitude && !form.longitude) ||
      (!form.latitude && form.longitude)
    ) {
      setError("Latitude and longitude must be supplied together.");
      return;
    }
    setUploading(true);
    setError("");
    setMessage("");
    try {
      const body = new FormData();
      body.append("file", file);
      Object.entries(form).forEach(([k, v]) => {
        if (v)
          body.append(k, k === "capturedAt" ? new Date(v).toISOString() : v);
      });
      const result = (
        await api.post<ImageReceipt>(`/flights/${id}/images`, body)
      ).data;
      setReceipt(result);
      setStatus({
        imageId: result.imageId,
        status: result.processingStatus,
        requestId: null,
        attemptCount: 1,
        queuedAt: null,
        startedAt: null,
        completedAt: null,
        errorCode: null,
        errorMessage: null,
      });
      setMessage("Upload accepted. Processing is being monitored.");
      setFile(null);
      if (fileRef.current) fileRef.current.value = "";
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setUploading(false);
    }
  };
  const retry = async () => {
    if (!status) return;
    try {
      const next = (
        await api.post<ProcessingStatus>(
          `/images/${status.imageId}/retry-processing`,
        )
      ).data;
      setStatus(next);
      setReceipt((r) => (r ? { ...r, imageId: next.imageId } : null));
      setMessage("Retry accepted; the stored file was not uploaded again.");
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
                <h2>Upload image for AI processing</h2>
                <form className="form" onSubmit={upload}>
                  <label className="upload-zone">
                    Image (.tif/.tiff; PNG/JPEG accepted in Development)
                    <input
                      ref={fileRef}
                      type="file"
                      accept=".tif,.tiff,.png,.jpg,.jpeg,image/tiff,image/png,image/jpeg"
                      onChange={(e) => setFile(e.target.files?.[0] || null)}
                      required
                    />
                  </label>
                  <div className="field-row three">
                    <label>
                      Captured at
                      <input
                        type="datetime-local"
                        value={form.capturedAt}
                        onChange={(e) =>
                          setForm({ ...form, capturedAt: e.target.value })
                        }
                        required
                      />
                    </label>
                    <label>
                      Latitude
                      <input
                        type="number"
                        step="any"
                        value={form.latitude}
                        onChange={(e) =>
                          setForm({ ...form, latitude: e.target.value })
                        }
                      />
                    </label>
                    <label>
                      Longitude
                      <input
                        type="number"
                        step="any"
                        value={form.longitude}
                        onChange={(e) =>
                          setForm({ ...form, longitude: e.target.value })
                        }
                      />
                    </label>
                    <label>
                      Altitude (m)
                      <input
                        type="number"
                        step="any"
                        value={form.altitudeM}
                        onChange={(e) =>
                          setForm({ ...form, altitudeM: e.target.value })
                        }
                      />
                    </label>
                    <label>
                      Heading (°)
                      <input
                        type="number"
                        step="any"
                        value={form.cameraHeadingDeg}
                        onChange={(e) =>
                          setForm({ ...form, cameraHeadingDeg: e.target.value })
                        }
                      />
                    </label>
                    <label>
                      GPS accuracy (cm)
                      <input
                        type="number"
                        step="any"
                        value={form.gpsAccuracyCm}
                        onChange={(e) =>
                          setForm({ ...form, gpsAccuracyCm: e.target.value })
                        }
                      />
                    </label>
                  </div>
                  <label>
                    <span>
                      Mock scenario{" "}
                      <em className="mock-label">Development-only</em>
                    </span>
                    <select
                      value={form.mockScenario}
                      onChange={(e) =>
                        setForm({ ...form, mockScenario: e.target.value })
                      }
                    >
                      <option value="none">None</option>
                      <option value="building">Building</option>
                      <option value="road_encroachment">
                        Road encroachment
                      </option>
                      <option value="parcel_encroachment">
                        Parcel encroachment
                      </option>
                      <option value="failure">Failure/retry check</option>
                    </select>
                  </label>
                  <button className="primary" disabled={uploading}>
                    <Upload size={14} />
                    {uploading ? "Uploading…" : "Upload and process"}
                  </button>
                </form>
                {receipt && (
                  <div className="detail" style={{ marginTop: 14 }}>
                    <small>Accepted image ID</small>
                    <ShortId value={receipt.imageId} />
                    <p>
                      <Status
                        value={status?.status || receipt.processingStatus}
                      />
                    </p>
                    {status?.errorMessage && (
                      <Notice kind="error">{status.errorMessage}</Notice>
                    )}
                    {status &&
                      ["AnalysisFailed", "ComplianceFailed"].includes(
                        status.status,
                      ) && (
                        <button
                          className="secondary"
                          onClick={() => void retry()}
                        >
                          <RotateCcw size={14} />
                          Retry stored image
                        </button>
                      )}
                  </div>
                )}
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
                            </td>
                            <td>{x.attemptCount}</td>
                            <td>{fmtDate(x.capturedAt)}</td>
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
              {status?.status === "Completed" && (
                <Card>
                  <h2>Processing results</h2>
                  <p>
                    <strong>{detections.length}</strong> AI Detection(s) ·{" "}
                    <strong>
                      {violations.filter((v) => v.flightId === id).length}
                    </strong>{" "}
                    Candidate Violation(s)
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
                  {violations
                    .filter((v) => v.flightId === id)
                    .map((v) => (
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
