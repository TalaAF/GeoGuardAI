import { ArrowLeft, CheckCircle2, RefreshCw, XCircle } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, ApiError, errorMessage } from "../api/client";
import type { ViolationDetails } from "../api/types";
import { hasRole, useAuth } from "../auth/AuthContext";
import {
  Card,
  Notice,
  PageTitle,
  ShortId,
  Spinner,
  Status,
  fmtDate,
  fmtPercent,
} from "../components/Common";

export default function ViolationDetail() {
  const { id } = useParams();
  const { user } = useAuth();
  const [data, setData] = useState<ViolationDetails | null>(null);
  const [error, setError] = useState("");
  const [notes, setNotes] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const load = useCallback(async () => {
    if (!id) return;
    setError("");
    try {
      setData((await api.get<ViolationDetails>(`/violations/${id}`)).data);
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
        description={id}
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
              <div className="detail-grid">
                <div className="detail">
                  <small>Status</small>
                  <Status value={data.status} />
                </div>
                <div className="detail">
                  <small>Type</small>
                  <strong>{data.violationType.replaceAll("_", " ")}</strong>
                </div>
                <div className="detail">
                  <small>AI confidence</small>
                  <strong className="confidence">
                    {fmtPercent(data.aiConfidenceScore)}
                  </strong>
                </div>
                <div className="detail">
                  <small>Detection</small>
                  <strong>
                    AI Detection · <ShortId value={data.detectionId} />
                  </strong>
                </div>
                <div className="detail">
                  <small>Detected class</small>
                  <strong>{data.detectionClass}</strong>
                </div>
                <div className="detail">
                  <small>Exceedance area</small>
                  <strong>
                    {data.exceedanceAreaM2 != null
                      ? `${data.exceedanceAreaM2.toFixed(2)} m²`
                      : "—"}
                  </strong>
                </div>
              </div>
            </Card>
            <Card>
              <h2>Related records</h2>
              <div className="detail-grid">
                <div className="detail">
                  <small>Flight</small>
                  <Link to={data.flightId ? `/flights/${data.flightId}` : "#"}>
                    <ShortId value={data.flightId} />
                  </Link>
                </div>
                <div className="detail">
                  <small>Parcel</small>
                  <ShortId value={data.parcelId} />
                </div>
                <div className="detail">
                  <small>Road</small>
                  <ShortId value={data.roadId} />
                </div>
                <div className="detail">
                  <small>Zone</small>
                  <ShortId value={data.zoneId} />
                </div>
                <div className="detail">
                  <small>Rule</small>
                  <ShortId value={data.ruleId} />
                </div>
                <div className="detail">
                  <small>Evidence image</small>
                  <ShortId value={data.evidenceImageId} />
                </div>
              </div>
              {data.evidenceStoragePath && (
                <p className="mono">{data.evidenceStoragePath}</p>
              )}
            </Card>
          </div>
          <div className="grid">
            <Card>
              <h2>Status history</h2>
              <div className="timeline">
                {data.statusHistory.map((h, i) => (
                  <div className="timeline-item" key={`${h.changedAt}-${i}`}>
                    <strong>{h.status.replaceAll("_", " ")}</strong>
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
