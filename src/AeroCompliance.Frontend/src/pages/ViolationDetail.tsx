import {
  ArrowLeft,
  CheckCircle2,
  Download,
  FileText,
  RefreshCw,
  XCircle,
} from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, ApiError, errorMessage } from "../api/client";
import type {
  ComplianceEvaluation,
  Detection,
  ReportSummary,
  ViolationDetails,
} from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { canGenerateViolationReport, canReviewViolations } from "../auth/capabilities";
import {
  Card,
  Notice,
  SourceBadge,
  PageTitle,
  Spinner,
  Status,
  fmtDate,
  fmtPercent,
} from "../components/Common";
import GeoMap, { type FeatureKind } from "../components/GeoMap";
import { APPROXIMATE_WARNING, MOCK_WARNING, aiClassLabel } from "../lib/provenance";
import { humanizeCode, shortRef } from "../lib/labels";
import { downloadReport } from "../lib/reports";
import ComplianceResults from "../components/ComplianceResults";

// Reports are only meaningful once a human has confirmed the violation; this mirrors the
// backend rule rather than replacing it (the API rejects anything else with a 409).
const CONFIRMED_STATUS = "confirmed_under_notification";

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
  const navigate = useNavigate();
  const { user } = useAuth();
  const [data, setData] = useState<ViolationDetails | null>(null);
  const [detection, setDetection] = useState<Detection | null>(null);
  const [error, setError] = useState("");
  const [notes, setNotes] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [report, setReport] = useState<ReportSummary | null>(null);
  const [reportBusy, setReportBusy] = useState(false);
  const [evaluations, setEvaluations] = useState<ComplianceEvaluation[]>([]);
  const load = useCallback(async () => {
    if (!id) return;
    setError("");
    try {
      const violation = (await api.get<ViolationDetails>(`/violations/${id}`)).data;
      setData(violation);
      setDetection((await api.get<Detection>(`/detections/${violation.detectionId}`)).data);
      // The rule comparison that produced this violation. A failure here must not hide the case.
      try {
        setEvaluations(
          (
            await api.get<ComplianceEvaluation[]>(
              `/detections/${violation.detectionId}/compliance-evaluations`,
            )
          ).data,
        );
      } catch {
        setEvaluations([]);
      }
      // Surface an already-generated report so the action reads "Download" rather than
      // silently regenerating. A failure here must not block the violation itself.
      if (violation.status === CONFIRMED_STATUS) {
        try {
          const existing = (
            await api.get<{ id: string; reference: string }[]>(
              `/violations/${violation.id}/reports`,
            )
          ).data;
          setReport(
            existing.length > 0 ? (existing[0] as unknown as ReportSummary) : null,
          );
        } catch {
          setReport(null);
        }
      } else {
        setReport(null);
      }
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
  const generateReport = async () => {
    if (!id || reportBusy) return; // guards a double click
    setReportBusy(true);
    setError("");
    try {
      const created = (await api.post<ReportSummary>(`/violations/${id}/reports`)).data;
      setReport(created);
      navigate(`/reports/${created.id}`);
    } catch (e) {
      setError(
        e instanceof ApiError && e.status === 409
          ? "A formal report can be generated after the violation is confirmed."
          : errorMessage(e),
      );
    } finally {
      setReportBusy(false);
    }
  };

  const downloadExisting = async () => {
    if (!report || reportBusy) return;
    setReportBusy(true);
    setError("");
    try {
      await downloadReport(api, report.id, report.reference);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setReportBusy(false);
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
              {/* Provenance is stated before any measurement, so an experimental or synthetic case
                  is never read as an authoritative municipal violation. */}
              <div className="badge-stack">
                <SourceBadge resultSource={data.resultSource} />
                {data.isExperimental && (
                  <span className="status status-under_review">Experimental candidate</span>
                )}
                {data.requiresManualReview && (
                  <span className="status status-under_review">Requires manual review</span>
                )}
              </div>
              {data.isApproximate && (
                <Notice kind="error">
                  <strong>Experimental candidate from estimated geometry.</strong>{" "}
                  {APPROXIMATE_WARNING} It is not an official or confirmed municipal violation.
                </Notice>
              )}
              {data.resultSource === "MockGeographicScenario" && (
                <Notice kind="info">
                  <strong>Deterministic development scenario.</strong> {MOCK_WARNING}
                </Notice>
              )}
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
                  <small>AI class</small>
                  <strong>{aiClassLabel(data.detectionClass, data.detectionClassId)}</strong>
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
            {evaluations.length > 0 && (
              <Card>
                <ComplianceResults items={evaluations} />
              </Card>
            )}
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
              canReviewViolations(user) && (
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
            {canGenerateViolationReport(user) && (
              <Card>
                <h2>Formal report</h2>
                {data.status !== CONFIRMED_STATUS ? (
                  <Notice>
                    A formal report can be generated after the violation is
                    confirmed.
                  </Notice>
                ) : (
                  <>
                    <p className="chart-note">
                      Produces a PDF compliance report from the reviewed record
                      and files it under Reports.
                    </p>
                    <div className="actions">
                      {report ? (
                        <>
                          <Link className="primary" to={`/reports/${report.id}`}>
                            <FileText size={14} />
                            View {report.reference}
                          </Link>
                          <button
                            className="secondary"
                            disabled={reportBusy}
                            onClick={() => void downloadExisting()}
                          >
                            <Download size={14} />
                            {reportBusy ? "Preparing…" : "Download PDF"}
                          </button>
                        </>
                      ) : (
                        <button
                          className="primary"
                          disabled={reportBusy}
                          onClick={() => void generateReport()}
                        >
                          <FileText size={14} />
                          {reportBusy ? "Generating report…" : "Generate report"}
                        </button>
                      )}
                    </div>
                  </>
                )}
              </Card>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
