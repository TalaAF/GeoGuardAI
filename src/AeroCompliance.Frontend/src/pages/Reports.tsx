import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Download, Search } from "lucide-react";
import { api, errorMessage, page as fetchPage } from "../api/client";
import type { Paged, ReportSummary } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { isPlatformAdmin } from "../auth/capabilities";
import {
  Card,
  Empty,
  Notice,
  Pager,
  PageTitle,
  Spinner,
  fmtDate,
} from "../components/Common";
import { downloadReport } from "../lib/reports";

export default function Reports() {
  const { user } = useAuth();
  const isAdmin = isPlatformAdmin(user);
  const [data, setData] = useState<Paged<ReportSummary> | null>(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [applied, setApplied] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      setData(
        await fetchPage<ReportSummary>("/reports", {
          page,
          pageSize: 25,
          search: applied || undefined,
        }),
      );
    } catch (e) {
      setData(null);
      setError(errorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [page, applied]);

  useEffect(() => {
    void load();
  }, [load]);

  const download = async (report: ReportSummary) => {
    setBusy(report.id);
    setError("");
    try {
      await downloadReport(api, report.id, report.reference);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="page">
      <PageTitle
        title="Reports"
        description="Formal compliance reports produced from confirmed violations."
      />

      <Card>
        <form
          className="filter-row"
          onSubmit={(e) => {
            e.preventDefault();
            setPage(1);
            setApplied(search.trim());
          }}
        >
          <label className="grow">
            Search
            <input
              type="search"
              value={search}
              placeholder="Report reference or violation type"
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <button type="submit" className="secondary">
            <Search size={14} /> Search
          </button>
          {applied && (
            <button
              type="button"
              className="secondary"
              onClick={() => {
                setSearch("");
                setApplied("");
                setPage(1);
              }}
            >
              Clear
            </button>
          )}
        </form>
      </Card>

      {error && <Notice kind="error">{error}</Notice>}

      <Card>
        {loading ? (
          <Spinner />
        ) : !data || data.items.length === 0 ? (
          <Empty
            title="No reports have been generated yet."
            detail="Open a confirmed violation and choose Generate report to produce one."
          />
        ) : (
          <>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Report</th>
                    {isAdmin && <th>Municipality</th>}
                    <th>Violation</th>
                    <th>Location</th>
                    <th>Status</th>
                    <th>Generated</th>
                    <th>Sign-off</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((r) => (
                    <tr key={r.id}>
                      <td>
                        <Link to={`/reports/${r.id}`}>{r.reference}</Link>
                      </td>
                      {isAdmin && <td>{r.municipalityName}</td>}
                      <td>
                        {r.violationType}
                        <small>{r.violationReference}</small>
                      </td>
                      <td>
                        {r.roadCode
                          ? r.roadName
                            ? `${r.roadCode} — ${r.roadName}`
                            : r.roadCode
                          : (r.parcelNumber ?? "—")}
                      </td>
                      <td>{r.violationStatus}</td>
                      <td>
                        {fmtDate(r.generatedAt)}
                        <small>{r.generatedBy}</small>
                      </td>
                      <td>
                        {r.signedOff ? (
                          <span className="badge-source">
                            {r.signedOffByName ?? "Signed off"}
                          </span>
                        ) : (
                          <span className="badge-source missing">
                            Not signed off
                          </span>
                        )}
                      </td>
                      <td>
                        <div className="actions">
                          <Link className="secondary" to={`/reports/${r.id}`}>
                            View
                          </Link>
                          <button
                            type="button"
                            className="secondary"
                            disabled={busy === r.id || !r.downloadAvailable}
                            onClick={() => void download(r)}
                          >
                            <Download size={13} />
                            {busy === r.id ? "Preparing…" : "Download PDF"}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pager
              page={data.page}
              totalPages={data.totalPages}
              onChange={setPage}
            />
          </>
        )}
      </Card>
    </div>
  );
}
