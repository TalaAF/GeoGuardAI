import {
  AlertTriangle,
  CheckCircle2,
  Image,
  Plane,
  RefreshCw,
  XCircle,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api, errorMessage } from "../api/client";
import type { DashboardSummary } from "../api/types";
import {
  Card,
  Empty,
  Notice,
  PageTitle,
  Spinner,
  Status,
  fmtDate,
  fmtPercent,
} from "../components/Common";
import { humanizeCode, shortRef } from "../lib/labels";

export default function Dashboard() {
  const [data, setData] = useState<DashboardSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      setData((await api.get<DashboardSummary>("/dashboard/summary")).data);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => {
    void load();
  }, [load]);
  if (loading && !data) return <Spinner />;
  const stats: [string, number, LucideIcon][] = data
    ? [
        ["Flights", data.totalFlights, Plane],
        ["Images", data.images, Image],
        ["Pending review", data.pendingViolations, AlertTriangle],
        ["Confirmed", data.confirmedViolations, CheckCircle2],
        ["Dismissed", data.dismissedViolations, XCircle],
      ]
    : [];
  return (
    <div className="page">
      <PageTitle
        title="Compliance Dashboard"
        description="Live operational totals from your authorized municipality scope."
        actions={
          <button className="secondary" onClick={() => void load()}>
            <RefreshCw size={14} />
            Refresh
          </button>
        }
      />
      {error && <Notice kind="error">{error}</Notice>}
      {data && (
        <>
          <div className="grid stats">
            {stats.map(([label, value, Icon]) => (
              <Card key={String(label)}>
                <div className="stat">
                  <div>
                    <small>{String(label)}</small>
                    <strong>{Number(value)}</strong>
                  </div>
                  <Icon size={18} />
                </div>
              </Card>
            ))}
          </div>
          <div className="grid two-col">
            <Card>
              <h2>Recent violations</h2>
              {data.recentViolations.length === 0 ? (
                <Empty
                  title="No violations"
                  detail="Candidate violations will appear after image analysis and compliance evaluation."
                />
              ) : (
                <div className="table-wrap">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Flight</th>
                        <th>Type</th>
                        <th>Confidence</th>
                        <th>Status</th>
                        <th>Created</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.recentViolations.map((v) => (
                        <tr key={v.id}>
                          <td>
                            <Link to={`/violations/${v.id}`}>
                              {v.flightMissionName || `Case ${shortRef(v.id)}`}
                            </Link>
                          </td>
                          <td>{humanizeCode(v.violationType)}</td>
                          <td className="confidence">
                            {fmtPercent(v.aiConfidenceScore)}
                          </td>
                          <td>
                            <Status value={v.status} />
                          </td>
                          <td>{fmtDate(v.createdAt)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </Card>
            <Card>
              <h2>Counts by status</h2>
              {data.countsByStatus.length === 0 ? (
                <Empty
                  title="No status data"
                  detail="There are no violation records in scope."
                />
              ) : (
                <div className="chart-bars">
                  {data.countsByStatus.map((x) => {
                    const max = Math.max(
                      ...data.countsByStatus.map((y) => y.count),
                      1,
                    );
                    return (
                      <div className="chart-row" key={x.status}>
                        <span>{x.status.replaceAll("_", " ")}</span>
                        <div className="chart-track">
                          <span
                            style={{ width: `${(x.count / max) * 100}%` }}
                          />
                        </div>
                        <strong>{x.count}</strong>
                      </div>
                    );
                  })}
                </div>
              )}
              <div className="detail" style={{ marginTop: 18 }}>
                <small>Processing completed</small>
                <strong>
                  {data.completedProcessingCount ?? "Not returned"}
                </strong>
              </div>
            </Card>
          </div>
        </>
      )}
    </div>
  );
}
