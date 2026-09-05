import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { RefreshCw, Search } from "lucide-react";
import { api, errorMessage, page as fetchPage } from "../api/client";
import type { ComplianceEvaluation, Paged, ViolationSummary } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { canReviewViolations } from "../auth/capabilities";
import {
  Card,
  Empty,
  Notice,
  SourceBadge,
  Pager,
  PageTitle,
  Spinner,
  Status,
  fmtDate,
} from "../components/Common";
import { humanizeCode } from "../lib/labels";

const STATUS_FILTERS: { value: string; label: string }[] = [
  { value: "", label: "All statuses" },
  { value: "under_review", label: "Awaiting review" },
  { value: "confirmed_under_notification", label: "Confirmed" },
  { value: "dismissed", label: "Dismissed" },
];

const SORTS: { value: string; label: string }[] = [
  { value: "newest", label: "Newest first" },
  { value: "oldest", label: "Oldest first" },
];

/** A violation row enriched with the rule comparison that produced it. */
interface QueueRow extends ViolationSummary {
  ruleResult?: string;
  location?: string;
}

export default function ReviewQueue() {
  const { user } = useAuth();
  const canDecide = canReviewViolations(user);

  const [data, setData] = useState<Paged<ViolationSummary> | null>(null);
  const [enriched, setEnriched] = useState<Record<string, QueueRow>>({});
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("under_review");
  const [sort, setSort] = useState("newest");
  const [search, setSearch] = useState("");
  const [applied, setApplied] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const result = await fetchPage<ViolationSummary>("/violations", {
        page,
        pageSize: 20,
        status: status || undefined,
        search: applied || undefined,
        from: from ? `${from}T00:00:00Z` : undefined,
        to: to ? `${to}T23:59:59Z` : undefined,
      });
      // Awaiting-review cases are surfaced first regardless of the chosen sort direction, so the
      // queue always opens on the work that needs a decision.
      const ordered = [...result.items].sort((a, b) => {
        const pending = (v: ViolationSummary) => (v.status === "under_review" ? 0 : 1);
        if (pending(a) !== pending(b)) return pending(a) - pending(b);
        const at = new Date(a.createdAt).getTime();
        const bt = new Date(b.createdAt).getTime();
        return sort === "oldest" ? at - bt : bt - at;
      });
      setData({ ...result, items: ordered });
    } catch (e) {
      setData(null);
      setError(errorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [page, status, applied, from, to, sort]);

  useEffect(() => {
    void load();
  }, [load]);

  // Pull the rule comparison for the visible page only. Failures leave the column blank rather
  // than breaking the queue.
  useEffect(() => {
    if (!data?.items.length) return;
    let cancelled = false;
    (async () => {
      const found: Record<string, QueueRow> = {};
      await Promise.all(
        data.items.map(async (v) => {
          try {
            const { data: details } = await api.get<{
              detectionId: string;
              roadCode: string | null;
              parcelNumber: string | null;
              zoneNumber: string | null;
            }>(`/violations/${v.id}`);
            const location =
              details.roadCode ?? details.parcelNumber ?? details.zoneNumber ?? undefined;
            const { data: evals } = await api.get<ComplianceEvaluation[]>(
              `/detections/${details.detectionId}/compliance-evaluations`,
            );
            const failed = evals.find((e) => e.outcome === "fail") ?? evals[0];
            found[v.id] = {
              ...v,
              location,
              ruleResult: failed
                ? `${failed.ruleLabel}: ${failed.measuredDisplay ?? "—"} measured / ${
                    failed.requiredDisplay ?? "—"
                  } required`
                : undefined,
            };
          } catch {
            found[v.id] = { ...v };
          }
        }),
      );
      if (!cancelled) setEnriched(found);
    })();
    return () => {
      cancelled = true;
    };
  }, [data]);

  const reset = () => {
    setSearch("");
    setApplied("");
    setFrom("");
    setTo("");
    setStatus("under_review");
    setPage(1);
  };

  return (
    <div className="page">
      <PageTitle
        title="Review Queue"
        description={
          canDecide
            ? "Candidate violations awaiting a review decision, newest first."
            : "Candidate violations awaiting review. Your role has read-only access."
        }
        actions={
          <button
            type="button"
            className="secondary"
            disabled={loading}
            onClick={() => void load()}
          >
            <RefreshCw size={14} /> Refresh
          </button>
        }
      />

      {!canDecide && (
        <Notice>
          You can open and inspect these cases, but confirming or dismissing a violation is
          restricted to Admin, Inspector, and Reviewer accounts.
        </Notice>
      )}

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
              placeholder="Violation type, flight, road, parcel, zone…"
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <label>
            Review status
            <select
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
            >
              {STATUS_FILTERS.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Sort
            <select value={sort} onChange={(e) => setSort(e.target.value)}>
              {SORTS.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            From
            <input type="date" value={from} max={to || undefined} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label>
            To
            <input type="date" value={to} min={from || undefined} onChange={(e) => setTo(e.target.value)} />
          </label>
          <button type="submit" className="secondary">
            <Search size={14} /> Apply
          </button>
          <button type="button" className="secondary" onClick={reset}>
            Reset
          </button>
        </form>
      </Card>

      {error && <Notice kind="error">{error}</Notice>}

      <Card>
        {loading ? (
          <Spinner />
        ) : !data || data.items.length === 0 ? (
          <Empty
            title="Nothing is waiting for review."
            detail="No violation matches these filters. Widen the date range or clear the status filter."
          />
        ) : (
          <>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Case</th>
                    <th>Detected issue</th>
                    <th>Flight</th>
                    <th>Location</th>
                    <th>Rule result</th>
                    <th>Detected</th>
                    <th>Review status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((v) => {
                    const row = enriched[v.id];
                    return (
                      <tr key={v.id}>
                        <td>
                          <Link to={`/violations/${v.id}`}>
                            {`ACV-${new Date(v.createdAt).getUTCFullYear()}-${v.id
                              .replace(/-/g, "")
                              .slice(0, 8)
                              .toUpperCase()}`}
                          </Link>
                        </td>
                        <td>
                          <div className="badge-stack">
                            <span>{humanizeCode(v.violationType)}</span>
                            <SourceBadge resultSource={v.resultSource} />
                            {v.isExperimental && (
                              <span className="status status-under_review">Experimental</span>
                            )}
                          </div>
                        </td>
                        <td>{v.flightMissionName || "—"}</td>
                        <td>{row?.location ?? "—"}</td>
                        <td>{row?.ruleResult ?? "—"}</td>
                        <td>{fmtDate(v.createdAt)}</td>
                        <td>
                          <Status value={v.status} />
                        </td>
                        <td>
                          <div className="actions">
                            <Link className="secondary" to={`/violations/${v.id}`}>
                              {canDecide && v.status === "under_review" ? "Review" : "View details"}
                            </Link>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <Pager page={data.page} totalPages={data.totalPages} onChange={setPage} />
          </>
        )}
      </Card>
    </div>
  );
}
