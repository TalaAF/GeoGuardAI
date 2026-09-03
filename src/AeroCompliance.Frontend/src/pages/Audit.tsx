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
  ShortId,
  Spinner,
  fmtDate,
} from "../components/Common";

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
        description="Immutable backend audit events within your authorized scope."
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
              placeholder="Entity GUID"
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
                    <th>Entity</th>
                    <th>Reference</th>
                    <th>Actor</th>
                    <th>Change</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((x) => (
                    <tr key={x.id}>
                      <td>{fmtDate(x.createdAt)}</td>
                      <td>{x.action}</td>
                      <td>{x.entityType}</td>
                      <td>
                        <ShortId value={x.entityId} />
                      </td>
                      <td>{x.actorName || "System"}</td>
                      <td>
                        <details>
                          <summary>View</summary>
                          <pre className="code">
                            {x.afterState ||
                              x.beforeState ||
                              "No serialized state"}
                          </pre>
                        </details>
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
