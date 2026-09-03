import { RefreshCw, Search } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { errorMessage, page as getPage } from "../api/client";
import type { AuditEntry, Paged } from "../api/types";
import {
  Card,
  Empty,
  Notice,
  PageTitle,
  Pager,
  Spinner,
  fmtDate,
} from "../components/Common";
import { deriveEntityReference, diffAuditState } from "../lib/audit";
import { humanizeCode } from "../lib/labels";

function ChangeList({ entry }: { entry: AuditEntry }) {
  const changes = diffAuditState(entry.beforeState, entry.afterState);
  if (!changes || changes.length === 0) {
    // Either nothing could be safely read as field-level changes, or nothing actually changed —
    // either way, the raw snapshot is never shown to the user.
    return <span className="muted">No further details recorded.</span>;
  }
  return (
    <div className="audit-changes">
      {changes.map((c) => (
        <div className="change-row" key={c.label}>
          <b>{c.label}:</b>
          {c.before === null ? (
            <span>{c.after}</span>
          ) : c.after === null ? (
            <span>{c.before} (removed)</span>
          ) : (
            <span>
              {humanizeCode(c.before)} → {humanizeCode(c.after)}
            </span>
          )}
        </div>
      ))}
    </div>
  );
}

export default function Audit() {
  const [data, setData] = useState<Paged<AuditEntry> | null>(null);
  const [p, setP] = useState(1);
  const [action, setAction] = useState("");
  const [entityType, setEntityType] = useState("");
  const [entityId, setEntityId] = useState("");
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    try {
      setData(
        await getPage<AuditEntry>("/audit", {
          page: p,
          pageSize: 25,
          action: action || undefined,
          entityType: entityType || undefined,
          entityId: entityId || undefined,
        }),
      );
    } catch (e) {
      setError(errorMessage(e));
    }
  }, [p, action, entityType, entityId]);
  useEffect(() => {
    void load();
  }, [load]);
  return (
    <div className="page">
      <PageTitle
        title="Audit Trail"
        description="A human-readable history of changes within your authorized scope."
        actions={
          <button className="secondary" onClick={() => void load()}>
            <RefreshCw size={14} />
            Refresh
          </button>
        }
      />
      <Card>
        <div className="toolbar">
          <input
            placeholder="Exact action"
            value={action}
            onChange={(e) => {
              setP(1);
              setAction(e.target.value);
            }}
          />
          <input
            placeholder="Entity type"
            value={entityType}
            onChange={(e) => {
              setP(1);
              setEntityType(e.target.value);
            }}
          />
          <div style={{ position: "relative" }}>
            <Search
              size={13}
              style={{ position: "absolute", left: 9, top: 10 }}
            />
            <input
              style={{ paddingLeft: 28 }}
              placeholder="Entity reference (id)"
              value={entityId}
              onChange={(e) => {
                setP(1);
                setEntityId(e.target.value);
              }}
            />
          </div>
        </div>
        {error && <Notice kind="error">{error}</Notice>}
        {!data ? (
          <Spinner />
        ) : data.items.length === 0 ? (
          <Empty
            title="No audit entries"
            detail="No events match the selected filters."
          />
        ) : (
          <>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Time</th>
                    <th>Action</th>
                    <th>Entity type</th>
                    <th>Reference</th>
                    <th>Actor</th>
                    <th>Change</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((x) => (
                    <tr key={x.id}>
                      <td>{fmtDate(x.createdAt)}</td>
                      <td>{humanizeCode(x.action)}</td>
                      <td>{x.entityType}</td>
                      <td>
                        {deriveEntityReference(x.entityType, x.beforeState, x.afterState, x.entityId)}
                      </td>
                      <td>{x.actorName || "System"}</td>
                      <td>
                        <ChangeList entry={x} />
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
