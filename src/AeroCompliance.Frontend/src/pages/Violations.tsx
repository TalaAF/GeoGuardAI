import { RefreshCw, Search } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { errorMessage, page as getPage } from "../api/client";
import type { Paged, ViolationSummary } from "../api/types";
import {
  Card,
  Empty,
  Notice,
  PageTitle,
  Pager,
  ShortId,
  Spinner,
  Status,
  fmtDate,
  fmtPercent,
} from "../components/Common";

export default function Violations() {
  const [data, setData] = useState<Paged<ViolationSummary> | null>(null);
  const [p, setP] = useState(1);
  const [status, setStatus] = useState("");
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    setError("");
    try {
      setData(
        await getPage<ViolationSummary>("/violations", {
          page: p,
          pageSize: 20,
          status: status || undefined,
          search: search || undefined,
        }),
      );
    } catch (e) {
      setError(errorMessage(e));
    }
  }, [p, status, search]);
  useEffect(() => {
    void load();
  }, [load]);
  return (
    <div className="page">
      <PageTitle
        title="Potential Violations"
        description="AI-supported candidate cases. A detection is not confirmed until reviewed."
        actions={
          <button className="secondary" onClick={() => void load()}>
            <RefreshCw size={14} />
            Refresh
          </button>
        }
      />
      <Card>
        <div className="toolbar">
          <div style={{ position: "relative" }}>
            <Search
              size={14}
              style={{
                position: "absolute",
                left: 10,
                top: 10,
                color: "#94a3b8",
              }}
            />
            <input
              style={{ paddingLeft: 30 }}
              placeholder="Search reference or type"
              value={search}
              onChange={(e) => {
                setP(1);
                setSearch(e.target.value);
              }}
            />
          </div>
          <select
            value={status}
            onChange={(e) => {
              setP(1);
              setStatus(e.target.value);
            }}
          >
            <option value="">All statuses</option>
            <option value="under_review">Under review</option>
            <option value="confirmed_under_notification">Confirmed</option>
            <option value="dismissed">Dismissed</option>
          </select>
        </div>
        {error && <Notice kind="error">{error}</Notice>}
        {!data ? (
          <Spinner />
        ) : data.items.length === 0 ? (
          <Empty
            title="No matching violations"
            detail="Change the filters or process a flight image to create candidate results."
          />
        ) : (
          <>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Reference</th>
                    <th>Type</th>
                    <th>Flight</th>
                    <th>AI confidence</th>
                    <th>Status</th>
                    <th>Created</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((v) => (
                    <tr key={v.id}>
                      <td>
                        <ShortId value={v.id} />
                      </td>
                      <td>{v.violationType.replaceAll("_", " ")}</td>
                      <td>
                        <ShortId value={v.flightId} />
                      </td>
                      <td className="confidence">
                        {fmtPercent(v.aiConfidenceScore)}
                      </td>
                      <td>
                        <Status value={v.status} />
                      </td>
                      <td>{fmtDate(v.createdAt)}</td>
                      <td>
                        <Link className="secondary" to={`/violations/${v.id}`}>
                          Open
                        </Link>
                      </td>
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
    </div>
  );
}
