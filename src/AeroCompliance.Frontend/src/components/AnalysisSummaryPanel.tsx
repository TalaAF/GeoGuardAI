import { useEffect, useState } from "react";
import { api, errorMessage } from "../api/client";
import type { AnalysisSummary, ProcessingStatus } from "../api/types";
import { Notice, Spinner, fmtDate } from "./Common";
import { shortRef } from "../lib/labels";

/**
 * What one image's analysis actually produced.
 *
 * Model-only detections are shown as their own figure rather than folded into a single total: they
 * are real predictions the model made, but they were deliberately left without a map position, and
 * a combined count would hide that. Fetched on demand so opening a flight stays a single request.
 */
export default function AnalysisSummaryPanel({ imageId }: { imageId: string }) {
  const [summary, setSummary] = useState<AnalysisSummary | null>(null);
  const [status, setStatus] = useState<ProcessingStatus | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError("");
    api
      .get<ProcessingStatus>(`/images/${imageId}/processing-status`)
      .then(({ data }) => {
        if (cancelled) return;
        setStatus(data);
        setSummary(data.analysis ?? null);
      })
      .catch((e) => !cancelled && setError(errorMessage(e)))
      .finally(() => !cancelled && setLoading(false));
    return () => {
      cancelled = true;
    };
  }, [imageId]);

  if (loading) return <Spinner />;
  if (error) return <Notice kind="error">{error}</Notice>;
  if (!summary) {
    return (
      <Notice kind="info">
        No analysis has run for this image yet
        {status?.status ? ` (status: ${status.status})` : ""}.
      </Notice>
    );
  }

  const estimated = summary.geographicStatus === "Estimated";

  return (
    <div className="form">
      <div className="badge-stack">
        <span className={`source-badge ${estimated ? "source-estimated" : "source-real"}`}>
          {summary.mode === "mock" ? "Mock analysis" : "Real YOLO analysis"}
        </span>
        <span className="status">{`Georeferencing: ${summary.geographicStatus}`}</span>
        <span className="status">{summary.geographicEvaluationEligibility}</span>
      </div>

      {/* A successful analysis with zero violations is still a successful analysis. */}
      {summary.status === "Completed" && summary.candidateViolationCount === 0 && (
        <Notice kind="success">
          Analysis completed successfully. No candidate violation was produced.
        </Notice>
      )}

      <div className="count-strip">
        <div className="count">
          <small>Total AI detections</small>
          <strong>{summary.totalDetections}</strong>
        </div>
        <div className="count">
          <small>Evaluated geographically</small>
          <strong>{summary.geographicDetections}</strong>
        </div>
        <div className="count">
          <small>Retained as model-only</small>
          <strong>{summary.modelOnlyDetections}</strong>
        </div>
        <div className="count">
          <small>Candidate violations</small>
          <strong>{summary.candidateViolationCount}</strong>
        </div>
      </div>

      {summary.geographicSkipReason && (
        <Notice kind="info">{summary.geographicSkipReason}</Notice>
      )}

      {estimated && (
        <Notice kind="error">
          Georeferencing for this image is <strong>estimated</strong>
          {summary.georeferencingMethod ? ` (${summary.georeferencingMethod})` : ""}. Only detections
          inside the trusted
          {summary.trustedRoiRadiusM != null ? ` ${summary.trustedRoiRadiusM} m ` : " "}
          region were evaluated geographically. Results are experimental and are not suitable for
          cadastral, surveying, enforcement, or legal use.
        </Notice>
      )}

      <div className="detail-grid">
        <div className="detail">
          <small>Analysis mode</small>
          <strong>{summary.mode}</strong>
        </div>
        <div className="detail">
          <small>Model</small>
          <strong className="cell-wrap">
            {summary.modelName || "Not reported"}
            {summary.modelVersion ? ` · ${summary.modelVersion}` : ""}
          </strong>
        </div>
        <div className="detail">
          <small>Analysis status</small>
          <strong>{summary.status}</strong>
        </div>
        {/* Distinguishes repeated uploads of the same filename from one another. */}
        <div className="detail">
          <small>Analysis reference</small>
          <strong>{shortRef(summary.analysisId)}</strong>
        </div>
        <div className="detail">
          <small>Processing request</small>
          <strong>{summary.requestId ? shortRef(summary.requestId) : "Not reported"}</strong>
        </div>
        <div className="detail">
          <small>Completed</small>
          <strong>{fmtDate(summary.completedAt)}</strong>
        </div>
        {summary.georeferencingConfidence && (
          <div className="detail">
            <small>Georeferencing confidence</small>
            <strong>{summary.georeferencingConfidence}</strong>
          </div>
        )}
        {summary.outsideTrustedRoiCount > 0 && (
          <div className="detail">
            <small>Outside trusted ROI</small>
            <strong>{summary.outsideTrustedRoiCount}</strong>
          </div>
        )}
      </div>

      {summary.modelOnlyDetections > 0 && (
        <Notice kind="info">
          {summary.modelOnlyDetections} model-only detection(s) are recorded on this analysis without
          geographic geometry. They are intentionally not drawn on the map and no coordinates are
          inferred for them.
        </Notice>
      )}
    </div>
  );
}
