import { ArrowLeft, CheckCircle2, RefreshCw, XCircle } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, ApiError, errorMessage } from "../api/client";
import type { Detection, ViolationDetails } from "../api/types";
import { hasRole, useAuth } from "../auth/AuthContext";
import {
  Card,
  Notice,
  PageTitle,
  Spinner,
  Status,
  fmtDate,
  fmtPercent,
} from "../components/Common";
import GeoMap, { type FeatureKind } from "../components/GeoMap";
import { humanizeCode, shortRef } from "../lib/labels";

// Detection != violation: the map style below always distinguishes the raw AI proposal (never shown
// here — this page only ever shows a *reviewed* candidate) from where that candidate currently
// stands in the confirm/dismiss workflow.
function statusToFeatureKind(status: string): FeatureKind {
  if (status === "under_review") return "candidate";
  if (status === "dismissed") return "dismissed";
  return "confirmed"; // confirmed_under_notification, notified, under_dispute, paused, resolved
}

export default function ViolationDetail() {
  const { id } = useParams();
  const { user } = useAuth();
  const [data, setData] = useState<ViolationDetails | null>(null);
  const [detection, setDetection] = useState<Detection | null>(null);
  const [error, setError] = useState("");
  const [notes, setNotes] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const load = useCallback(async () => {
    if (!id) return;
    setError("");
    try {
      const violation = (await api.get<ViolationDetails>(`/violations/${id}`)).data;
      setData(violation);
      setDetection((await api.get<Detection>(`/detections/${violation.detectionId}`)).data);
    } catch (e) {
      setError(errorMessage(e));
    }
  }, [id]);
  useEffect(() => {
    void load();
  }, [load]);
  const decide = async (action: "confirm" | "dismiss") => {
    if (!notes.trim()) {
      setError(
        action === "dismiss"
          ? "A dismissal reason is required."
          : "Review notes are required.",
      );
      return;
    }
    setBusy(true);
    setError("");
    try {
      await api.post(`/violations/${id}/${action}`, { notes: notes.trim() });
      setMessage(
        action === "confirm"
          ? "Violation confirmed successfully."
          : "Violation dismissed successfully.",
      );
      setNotes("");
      await load();
    } catch (e) {
      setError(
        e instanceof ApiError && e.status === 409
          ? `Conflict: ${e.message}`
          : errorMessage(e),
      );
    } finally {
      setBusy(false);
    }
  };
  if (!data && !error) return <Spinner />;
  return (
    <div className="page">
      <PageTitle
        title="Violation Case"
        description={id ? `Case ${shortRef(id)}` : undefined}
        actions={
          <>
            <Link className="secondary" to="/violations">
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
      {data && (
        <div className="grid two-col">
          <div className="grid">
            <Card>
              <h2>Candidate violation</h2>
              <Notice>
                {data.status === "under_review"
                  ? "AI detection — not yet a confirmed violation. Review the evidence below before deciding."
                  : `This candidate has been reviewed: ${humanizeCode(data.status)}.`}
              </Notice>
              <div className="detail-grid">
                <div className="detail">
                  <small>Status</small>
                  <Status value={data.status} />
                </div>
                <div className="detail">
                  <small>Type</small>
                  <strong>{humanizeCode(data.violationType)}</strong>
                </div>
                <div className="detail">
                  <small>AI confidence</small>
                  <strong className="confidence">
                    {fmtPercent(data.aiConfidenceScore)}
                  </strong>
                </div>
                <div className="detail">
                  <small>Detected class</small>
                  <strong>{data.detectionClass || "Unclassified"}</strong>
                </div>
                <div className="detail">
                  <small>Source image</small>
                  <strong>{data.sourceImageFileName || "Not available"}</strong>
                </div>
                <div className="detail">
                  <small>Flight</small>
                  {data.flightId ? (
                    <Link to={`/flights/${data.flightId}`}>
                      {data.flightMissionName || "Untitled flight"}
                    </Link>
                  ) : (
                    <strong>Not available</strong>
                  )}
                </div>
                <div className="detail">
                  <small>Exceedance area</small>
                  <strong>
                    {data.exceedanceAreaM2 != null
                      ? `${data.exceedanceAreaM2.toFixed(2)} m²`
                      : "—"}
                  </strong>
                </div>
                <div className="detail">
                  <small>Reference</small>
                  <strong>Detection {shortRef(data.detectionId)}</strong>
                </div>
              </div>
            </Card>
            {detection?.geometry != null ? (
              <Card>
                <h2>Boundary</h2>
                <GeoMap
                  features={[
                    {
                      id: data.detectionId,
                      geometry: detection.geometry,
                      kind: statusToFeatureKind(data.status),
                      label: `${humanizeCode(data.violationType)} · ${humanizeCode(data.status)}`,
                    },
                  ]}
                  legend
                />
              </Card>
            ) : (
              <Notice kind="info">No geographic boundary is available for this detection.</Notice>
            )}
            <Card>
              <h2>Related records</h2>
              <div className="detail-grid">
                <div className="detail">
                  <small>Parcel</small>
                  <strong>{data.parcelNumber ? `Parcel ${data.parcelNumber}` : "Not applicable"}</strong>
                </div>
                <div className="detail">
                  <small>Road</small>
                  <strong>{data.roadCode ? `Road ${data.roadCode}` : "Not applicable"}</strong>
                </div>
                <div className="detail">
                  <small>Zone</small>
                  <strong>{data.zoneNumber ? `Zone ${data.zoneNumber}` : "Not applicable"}</strong>
                </div>
                <div className="detail">
                  <small>Block</small>
                  <strong>{data.blockNumber ? `Block ${data.blockNumber}` : "Not applicable"}</strong>
                </div>
                <div className="detail">
                  <small>Zoning rule</small>
                  <strong>{data.ruleKey || "Not applicable"}</strong>
                </div>
                <div className="detail">
                  <small>Evidence image</small>
                  <strong>{data.evidenceImageId ? "Available" : "Not attached"}</strong>
                </div>
              </div>
            </Card>
          </div>
          <div className="grid">
            <Card>
              <h2>Status history</h2>
              <div className="timeline">
                {data.statusHistory.map((h, i) => (
                  <div className="timeline-item" key={`${h.changedAt}-${i}`}>
                    <strong>{humanizeCode(h.status)}</strong>
                    <p>{h.notes || "No notes"}</p>
                    <small>{fmtDate(h.changedAt)}</small>
                  </div>
                ))}
              </div>
            </Card>
            {data.status === "under_review" &&
              hasRole(user, ["Admin", "Reviewer", "Inspector"]) && (
                <Card>
                  <h2>Review decision</h2>
                  <div className="form">
                    <label>
                      Decision notes
                      <textarea
                        value={notes}
                        onChange={(e) => setNotes(e.target.value)}
                        placeholder="Record the evidence and rationale…"
                        maxLength={2000}
                      />
                    </label>
                    <div className="actions">
                      <button
                        className="primary"
                        disabled={busy}
                        onClick={() => void decide("confirm")}
                      >
                        <CheckCircle2 size={14} />
                        Confirm
                      </button>
                      <button
                        className="danger"
                        disabled={busy}
                        onClick={() => void decide("dismiss")}
                      >
                        <XCircle size={14} />
                        Dismiss
                      </button>
                    </div>
                  </div>
                </Card>
              )}
          </div>
        </div>
      )}
    </div>
  );
}
