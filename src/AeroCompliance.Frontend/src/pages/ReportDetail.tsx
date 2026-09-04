import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, Download } from "lucide-react";
import { api, errorMessage } from "../api/client";
import type { ReportDetails } from "../api/types";
import {
  Card,
  Empty,
  Notice,
  PageTitle,
  Spinner,
  fmtDate,
  fmtPercent,
} from "../components/Common";
import GeoMap from "../components/GeoMap";
import { downloadReport } from "../lib/reports";

/**
 * Label/value pair using the app's existing `.detail` card styling, showing an explicit
 * fallback rather than an empty cell.
 */
function Field({
  label,
  value,
}: {
  label: string;
  value: string | number | null | undefined;
}) {
  const missing = value == null || value === "";
  return (
    <div className="detail">
      <small>{label}</small>
      <strong className={missing ? "muted-note" : undefined}>
        {missing ? "Not recorded" : value}
      </strong>
    </div>
  );
}

const measure = (value: number | null, unit: string | null) =>
  value == null ? null : unit ? `${value} ${unit}` : String(value);

export default function ReportDetail() {
  const { id = "" } = useParams();
  const [report, setReport] = useState<ReportDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [downloading, setDownloading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const { data } = await api.get<ReportDetails>(`/reports/${id}`);
      setReport(data);
    } catch (e) {
      setReport(null);
      setError(errorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  const download = async () => {
    if (!report) return;
    setDownloading(true);
    setError("");
    try {
      await downloadReport(api, report.id, report.reference);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setDownloading(false);
    }
  };

  if (loading) return <div className="page"><Card><Spinner /></Card></div>;
  if (!report)
    return (
      <div className="page">
        <Card>
          <Empty
            title="This report could not be opened."
            detail={error || "It may have been removed, or it belongs to another municipality."}
          />
          <Link className="secondary" to="/reports">
            Back to reports
          </Link>
        </Card>
      </div>
    );

  return (
    <div className="page">
      <PageTitle
        title={report.reference}
        description={`${report.violationType} · ${report.municipalityName}`}
        actions={
          <>
            <Link className="secondary" to="/reports">
              <ArrowLeft size={14} /> All reports
            </Link>
            <button
              type="button"
              className="primary"
              disabled={downloading || !report.downloadAvailable}
              onClick={() => void download()}
            >
              <Download size={14} />
              {downloading ? "Preparing…" : "Download PDF"}
            </button>
          </>
        }
      />

      {error && <Notice kind="error">{error}</Notice>}

      <Card>
        <h2 className="chart-title">Report summary</h2>
        <div className="detail-grid">
          <Field label="Report reference" value={report.reference} />
          <Field label="Municipality" value={report.municipalityName} />
          <Field label="Generated" value={fmtDate(report.generatedAt)} />
          <Field label="Source" value={report.generatedBy} />
          <Field
            label="Sign-off"
            value={
              report.signedOff
                ? `${report.signedOffByName ?? "Signed off"} · ${fmtDate(report.signedOffAt)}`
                : "Not signed off"
            }
          />
        </div>
      </Card>

      <Card>
        <h2 className="chart-title">Violation</h2>
        <div className="detail-grid">
          <Field label="Violation reference" value={report.violationReference} />
          <Field label="Violation type" value={report.violationType} />
          <Field label="Current status" value={report.violationStatusLabel} />
          <Field label="Detection class" value={report.detectionClass} />
          <Field
            label="AI confidence"
            value={fmtPercent(report.detectionConfidence)}
          />
          <Field label="Flight mission" value={report.flightMissionName} />
          <Field label="Source image" value={report.sourceImageFileName} />
        </div>
        <p className="chart-note">
          <Link to={`/violations/${report.violationId}`}>
            Open the full violation record
          </Link>
        </p>
      </Card>

      <Card>
        <h2 className="chart-title">Location</h2>
        <div className="detail-grid">
          <Field
            label="Road"
            value={
              report.roadCode
                ? report.roadName
                  ? `${report.roadCode} — ${report.roadName}`
                  : report.roadCode
                : null
            }
          />
          <Field label="Parcel number" value={report.parcelNumber} />
          <Field label="Zone number" value={report.zoneNumber} />
          <Field label="Block number" value={report.blockNumber} />
        </div>
      </Card>

      <Card>
        <h2 className="chart-title">Compliance evaluation</h2>
        <div className="detail-grid">
          <Field label="Evaluation type" value={report.evaluationType} />
          <Field
            label="Measured value"
            value={measure(report.measuredValue, report.unit)}
          />
          <Field
            label="Required value"
            value={measure(report.requiredValue, report.unit)}
          />
          <Field
            label="Exceedance"
            value={measure(report.exceedanceValue, report.unit)}
          />
          <Field
            label="Encroachment area"
            value={
              report.exceedanceAreaM2 == null
                ? null
                : `${report.exceedanceAreaM2} m²`
            }
          />
        </div>
      </Card>

      <Card>
        <h2 className="chart-title">Human review</h2>
        <div className="detail-grid">
          <Field label="Decision" value={report.violationStatusLabel} />
          <Field label="Reviewer" value={report.reviewedByName} />
          <Field
            label="Reviewed at"
            value={report.reviewedAt ? fmtDate(report.reviewedAt) : null}
          />
        </div>
        <div className="detail review-notes">
          <small>Notes</small>
          <strong className={report.reviewNotes ? undefined : "muted-note"}>
            {report.reviewNotes || "No review notes were recorded."}
          </strong>
        </div>
      </Card>

      {report.statusHistory.length > 0 && (
        <Card>
          <h2 className="chart-title">Status history</h2>
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Status</th>
                  <th>Changed by</th>
                  <th>Notes</th>
                </tr>
              </thead>
              <tbody>
                {report.statusHistory.map((h, i) => (
                  <tr key={`${h.changedAt}-${i}`}>
                    <td>{fmtDate(h.changedAt)}</td>
                    <td>{h.statusLabel}</td>
                    <td>{h.actorName ?? "Automated"}</td>
                    <td>{h.notes || "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      <Card>
        <h2 className="chart-title">Location on map</h2>
        {report.geometry ? (
          <GeoMap
            features={[
              {
                id: report.violationId,
                geometry: report.geometry,
                kind: "confirmed",
                label: report.violationReference,
              },
            ]}
            height={320}
          />
        ) : (
          <p className="muted-note">
            No geographic boundary is available for this violation.
          </p>
        )}
      </Card>

      <Card>
        <p className="chart-note">
          AI detections are decision-support evidence. The recorded violation
          outcome reflects human review in AeroCompliance.
        </p>
      </Card>
    </div>
  );
}
