import { Plus, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { api, errorMessage, page as getPage } from "../api/client";
import type {
  FlightSummary,
  Municipality,
  Paged,
  UserInfo,
} from "../api/types";
import { hasRole, useAuth } from "../auth/AuthContext";
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
} from "../components/Common";
import GeometryEditor from "../components/GeometryEditor";
import type { NormalizedMultiPolygon } from "../lib/geometry";

export default function Flights() {
  const { user } = useAuth();
  const [data, setData] = useState<Paged<FlightSummary> | null>(null);
  const [operators, setOperators] = useState<UserInfo[]>([]);
  const [municipalities, setMunicipalities] = useState<Municipality[]>([]);
  const [p, setP] = useState(1);
  const [show, setShow] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    missionName: "",
    municipalityId: user?.municipality?.id || "",
    droneOperatorId: user?.role.name === "DroneOperator" ? user.id : "",
    scheduledAt: "",
    plannedArea: null as NormalizedMultiPolygon | null,
  });
  const load = useCallback(async () => {
    try {
      setData(
        await getPage<FlightSummary>("/flights", { page: p, pageSize: 20 }),
      );
      if (hasRole(user, ["Admin", "MunicipalityManager"]))
        setOperators(
          (
            await getPage<UserInfo>("/users", {
              pageSize: 100,
              role: "DroneOperator",
              municipalityId: form.municipalityId || undefined,
            })
          ).items,
        );
      if (user?.role.name === "Admin")
        setMunicipalities(
          (await getPage<Municipality>("/municipalities", { pageSize: 100 }))
            .items,
        );
    } catch (e) {
      setError(errorMessage(e));
    }
  }, [p, user, form.municipalityId]);
  useEffect(() => {
    void load();
  }, [load]);
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      await api.post("/flights", {
        municipalityId: form.municipalityId || undefined,
        droneOperatorId: form.droneOperatorId || undefined,
        missionName: form.missionName || null,
        scheduledAt: form.scheduledAt
          ? new Date(form.scheduledAt).toISOString()
          : null,
        plannedArea: form.plannedArea,
        qualityFlag: true,
      });
      setShow(false);
      setForm((x) => ({
        ...x,
        missionName: "",
        scheduledAt: "",
        plannedArea: null,
      }));
      await load();
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  };
  const canManage = hasRole(user, [
    "Admin",
    "MunicipalityManager",
    "DroneOperator",
  ]);
  return (
    <div className="page">
      <PageTitle
        title="Flights & Missions"
        description="Real flight records, missions, image uploads, and processing progress."
        actions={
          <>
            <button className="secondary" onClick={() => void load()}>
              <RefreshCw size={14} />
              Refresh
            </button>
            {canManage && (
              <button className="primary" onClick={() => setShow(!show)}>
                <Plus size={14} />
                New flight
              </button>
            )}
          </>
        }
      />
      {error && <Notice kind="error">{error}</Notice>}
      {show && (
        <Card>
          <h2>Create flight</h2>
          <form className="form" onSubmit={submit}>
            <div className="field-row three">
              <label>
                Mission name
                <input
                  value={form.missionName}
                  onChange={(e) =>
                    setForm({ ...form, missionName: e.target.value })
                  }
                />
              </label>
              {user?.role.name === "Admin" && (
                <label>
                  Municipality
                  <select
                    required
                    value={form.municipalityId}
                    onChange={(e) =>
                      setForm({
                        ...form,
                        municipalityId: e.target.value,
                        droneOperatorId: "",
                      })
                    }
                  >
                    <option value="">Select…</option>
                    {municipalities.map((x) => (
                      <option key={x.id} value={x.id}>
                        {x.name}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              {user?.role.name !== "DroneOperator" && (
                <label>
                  Drone operator
                  <select
                    required
                    value={form.droneOperatorId}
                    onChange={(e) =>
                      setForm({ ...form, droneOperatorId: e.target.value })
                    }
                  >
                    <option value="">Select…</option>
                    {operators.map((x) => (
                      <option key={x.id} value={x.id}>
                        {x.fullName}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              <label>
                Scheduled at
                <input
                  type="datetime-local"
                  value={form.scheduledAt}
                  onChange={(e) =>
                    setForm({ ...form, scheduledAt: e.target.value })
                  }
                />
              </label>
            </div>
            <label>
              Planned area (optional)
              <GeometryEditor
                onChange={(g) => setForm((f) => ({ ...f, plannedArea: g }))}
                boundaryLabel="planned area"
              />
            </label>
            <div>
              <button className="primary" disabled={busy}>
                {busy ? "Saving…" : "Create flight"}
              </button>
            </div>
          </form>
        </Card>
      )}
      <Card>
        {!data ? (
          <Spinner />
        ) : data.items.length === 0 ? (
          <Empty
            title="No flights"
            detail="Create the first authorized flight to begin image processing."
          />
        ) : (
          <>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Reference</th>
                    <th>Mission</th>
                    <th>Operator</th>
                    <th>Status</th>
                    <th>Scheduled</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((f) => (
                    <tr key={f.id}>
                      <td>
                        <ShortId value={f.id} />
                      </td>
                      <td>{f.missionName || "Untitled flight"}</td>
                      <td>
                        {f.operatorName || (
                          <ShortId value={f.droneOperatorId} />
                        )}
                      </td>
                      <td>
                        <Status value={f.status} />
                      </td>
                      <td>{fmtDate(f.scheduledAt)}</td>
                      <td>
                        <Link className="secondary" to={`/flights/${f.id}`}>
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
