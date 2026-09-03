import { CheckCircle2, Edit3, Plus, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { api, errorMessage, page as getPage } from "../api/client";
import type {
  Block,
  Municipality,
  Parcel,
  Road,
  Zone,
  ZoningRule,
} from "../api/types";
import { hasRole, useAuth } from "../auth/AuthContext";
import {
  Card,
  Empty,
  Notice,
  PageTitle,
  ShortId,
  Spinner,
} from "../components/Common";

type Tab = "zones" | "blocks" | "parcels" | "roads" | "zoning-rules";
type Row = Zone | Block | Parcel | Road | ZoningRule;
const blankGeometry = '{"type":"MultiPolygon","coordinates":[]}';
const initial = {
  municipalityId: "",
  zoneId: "",
  blockId: "",
  zoneNumber: "",
  blockNumber: "",
  parcelNumber: "",
  roadCode: "",
  name: "",
  zoneType: "",
  ownerName: "",
  areaM2: "",
  source: "municipality_gis",
  roadType: "",
  widthM: "",
  speedLimitKmh: "",
  isSensitive: false,
  ruleKey: "",
  ruleValue: "",
  unit: "m",
  sourceDocument: "",
  extractedByAi: false,
  isActive: true,
  geometry: blankGeometry,
};
export default function Gis() {
  const { user } = useAuth();
  const [tab, setTab] = useState<Tab>("zones");
  const [rows, setRows] = useState<Row[] | null>(null);
  const [selected, setSelected] = useState<Row | null>(null);
  const [zones, setZones] = useState<Zone[]>([]);
  const [blocks, setBlocks] = useState<Block[]>([]);
  const [municipalities, setMunicipalities] = useState<Municipality[]>([]);
  const [form, setForm] = useState({
    ...initial,
    municipalityId: user?.municipality?.id || "",
  });
  const [editing, setEditing] = useState<string | null>(null);
  const [show, setShow] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const canManage = hasRole(user, [
    "Admin",
    "MunicipalityManager",
    "GISAnalyst",
  ]);
  const load = useCallback(async () => {
    setError("");
    try {
      const [current, z, b] = await Promise.all([
        getPage<Row>(`/${tab}`, { pageSize: 100 }),
        getPage<Zone>("/zones", { pageSize: 100 }),
        getPage<Block>("/blocks", { pageSize: 100 }),
      ]);
      setRows(current.items);
      setZones(z.items);
      setBlocks(b.items);
      if (user?.role.name === "Admin")
        setMunicipalities(
          (await getPage<Municipality>("/municipalities", { pageSize: 100 }))
            .items,
        );
    } catch (e) {
      setError(errorMessage(e));
    }
  }, [tab, user]);
  useEffect(() => {
    setRows(null);
    void load();
  }, [load]);
  const reset = () => {
    setEditing(null);
    setForm({ ...initial, municipalityId: user?.municipality?.id || "" });
  };
  const save = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      let body: Record<string, unknown>;
      if (tab === "zones")
        body = {
          municipalityId: form.municipalityId || undefined,
          zoneNumber: form.zoneNumber,
          name: form.name || null,
          zoneType: form.zoneType || null,
          geometry: JSON.parse(form.geometry),
          sourceDocument: form.sourceDocument || null,
        };
      else if (tab === "blocks")
        body = {
          zoneId: form.zoneId,
          blockNumber: form.blockNumber,
          geometry: JSON.parse(form.geometry),
        };
      else if (tab === "parcels")
        body = {
          blockId: form.blockId,
          parcelNumber: form.parcelNumber,
          ownerName: form.ownerName || null,
          zoneType: form.zoneType || null,
          areaM2: form.areaM2 ? Number(form.areaM2) : null,
          geometry: JSON.parse(form.geometry),
          source: form.source,
        };
      else if (tab === "roads")
        body = {
          municipalityId: form.municipalityId || undefined,
          roadCode: form.roadCode,
          name: form.name || null,
          roadType: form.roadType || null,
          widthM: form.widthM ? Number(form.widthM) : null,
          speedLimitKmh: form.speedLimitKmh ? Number(form.speedLimitKmh) : null,
          isSensitive: form.isSensitive,
          geometry: JSON.parse(form.geometry),
        };
      else
        body = {
          municipalityId: form.municipalityId || undefined,
          zoneId: form.zoneId || null,
          zoneType: form.zoneId ? null : form.zoneType || null,
          ruleKey: form.ruleKey,
          ruleValue: Number(form.ruleValue),
          unit: form.unit,
          sourceDocument: form.sourceDocument || null,
          extractedByAi: form.extractedByAi,
          isActive: form.isActive,
        };
      if (editing) await api.put(`/${tab}/${editing}`, body);
      else await api.post(`/${tab}`, body);
      setMessage(editing ? "GIS record updated." : "GIS record created.");
      setShow(false);
      reset();
      await load();
    } catch (e) {
      setError(
        e instanceof SyntaxError
          ? "Geometry must be valid GeoJSON using longitude then latitude."
          : errorMessage(e),
      );
    }
  };
  const edit = async (row: Row) => {
    try {
      row = (await api.get<Row>(`/${tab}/${row.id}`)).data;
    } catch (e) {
      setError(errorMessage(e));
      return;
    }
    const r = row as unknown as Record<string, unknown>;
    setEditing(String(r.id));
    setForm({
      ...initial,
      municipalityId: String(r.municipalityId || user?.municipality?.id || ""),
      zoneId: String(r.zoneId || ""),
      blockId: String(r.blockId || ""),
      zoneNumber: String(r.zoneNumber || ""),
      blockNumber: String(r.blockNumber || ""),
      parcelNumber: String(r.parcelNumber || ""),
      roadCode: String(r.roadCode || ""),
      name: String(r.name || ""),
      zoneType: String(r.zoneType || ""),
      ownerName: String(r.ownerName || ""),
      areaM2: String(r.areaM2 || ""),
      source: String(r.source || "municipality_gis"),
      roadType: String(r.roadType || ""),
      widthM: String(r.widthM || ""),
      speedLimitKmh: String(r.speedLimitKmh || ""),
      isSensitive: Boolean(r.isSensitive),
      ruleKey: String(r.ruleKey || ""),
      ruleValue: String(r.ruleValue || ""),
      unit: String(r.unit || "m"),
      sourceDocument: String(r.sourceDocument || ""),
      extractedByAi: Boolean(r.extractedByAi),
      isActive: r.isActive === undefined ? true : Boolean(r.isActive),
      geometry: r.geometry
        ? JSON.stringify(r.geometry, null, 2)
        : blankGeometry,
    });
    setShow(true);
  };
  const view = async (row: Row) => {
    try {
      setSelected((await api.get<Row>(`/${tab}/${row.id}`)).data);
    } catch (e) {
      setError(errorMessage(e));
    }
  };
  const validate = async (id: string) => {
    try {
      await api.post(`/zoning-rules/${id}/validate`);
      setMessage("Zoning rule validated.");
      await load();
    } catch (e) {
      setError(errorMessage(e));
    }
  };
  const title = (row: Row) => {
    const r = row as unknown as Record<string, unknown>;
    return String(
      r.zoneNumber ||
        r.blockNumber ||
        r.parcelNumber ||
        r.roadCode ||
        r.ruleKey ||
        r.id,
    );
  };
  return (
    <div className="page">
      <PageTitle
        title="GIS Records"
        description="Authoritative GeoJSON records. Coordinates remain longitude then latitude; CRS is never changed by the client."
        actions={
          <button className="secondary" onClick={() => void load()}>
            <RefreshCw size={14} />
            Refresh
          </button>
        }
      />
      {error && <Notice kind="error">{error}</Notice>}
      {message && <Notice kind="success">{message}</Notice>}
      <Card>
        <div className="tabs">
          {(
            ["zones", "blocks", "parcels", "roads", "zoning-rules"] as Tab[]
          ).map((x) => (
            <button
              key={x}
              className={tab === x ? "active" : ""}
              onClick={() => {
                setTab(x);
                setShow(false);
                setSelected(null);
                reset();
              }}
            >
              {x.replaceAll("-", " ")}
            </button>
          ))}
        </div>
        {canManage && (
          <button
            className="primary"
            onClick={() => {
              if (show) {
                setShow(false);
                reset();
              } else setShow(true);
            }}
          >
            <Plus size={14} />
            {show ? "Close" : "Create record"}
          </button>
        )}
        {show && (
          <form className="form" onSubmit={save} style={{ marginTop: 16 }}>
            <div className="field-row three">
              {user?.role.name === "Admin" &&
                ["zones", "roads", "zoning-rules"].includes(tab) && (
                  <label>
                    Municipality
                    <select
                      required
                      value={form.municipalityId}
                      onChange={(e) =>
                        setForm({ ...form, municipalityId: e.target.value })
                      }
                    >
                      <option value="">Select…</option>
                      {municipalities.map((x) => (
                        <option value={x.id} key={x.id}>
                          {x.name}
                        </option>
                      ))}
                    </select>
                  </label>
                )}
              {tab === "zones" && (
                <>
                  <label>
                    Zone number
                    <input
                      required
                      value={form.zoneNumber}
                      onChange={(e) =>
                        setForm({ ...form, zoneNumber: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Name
                    <input
                      value={form.name}
                      onChange={(e) =>
                        setForm({ ...form, name: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Zone type
                    <input
                      value={form.zoneType}
                      onChange={(e) =>
                        setForm({ ...form, zoneType: e.target.value })
                      }
                    />
                  </label>
                </>
              )}
              {tab === "blocks" && (
                <>
                  <label>
                    Parent zone
                    <select
                      required
                      value={form.zoneId}
                      onChange={(e) =>
                        setForm({ ...form, zoneId: e.target.value })
                      }
                    >
                      <option value="">Select…</option>
                      {zones.map((x) => (
                        <option key={x.id} value={x.id}>
                          {x.zoneNumber}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Block number
                    <input
                      required
                      value={form.blockNumber}
                      onChange={(e) =>
                        setForm({ ...form, blockNumber: e.target.value })
                      }
                    />
                  </label>
                </>
              )}
              {tab === "parcels" && (
                <>
                  <label>
                    Parent block
                    <select
                      required
                      value={form.blockId}
                      onChange={(e) =>
                        setForm({ ...form, blockId: e.target.value })
                      }
                    >
                      <option value="">Select…</option>
                      {blocks.map((x) => (
                        <option key={x.id} value={x.id}>
                          {x.blockNumber}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Parcel number
                    <input
                      required
                      value={form.parcelNumber}
                      onChange={(e) =>
                        setForm({ ...form, parcelNumber: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Owner name
                    <input
                      value={form.ownerName}
                      onChange={(e) =>
                        setForm({ ...form, ownerName: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Zone type
                    <input
                      value={form.zoneType}
                      onChange={(e) =>
                        setForm({ ...form, zoneType: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Area m²
                    <input
                      type="number"
                      value={form.areaM2}
                      onChange={(e) =>
                        setForm({ ...form, areaM2: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Source
                    <input
                      value={form.source}
                      onChange={(e) =>
                        setForm({ ...form, source: e.target.value })
                      }
                    />
                  </label>
                </>
              )}
              {tab === "roads" && (
                <>
                  <label>
                    Road code
                    <input
                      required
                      value={form.roadCode}
                      onChange={(e) =>
                        setForm({ ...form, roadCode: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Name
                    <input
                      value={form.name}
                      onChange={(e) =>
                        setForm({ ...form, name: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Road type
                    <input
                      value={form.roadType}
                      onChange={(e) =>
                        setForm({ ...form, roadType: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Width m
                    <input
                      type="number"
                      value={form.widthM}
                      onChange={(e) =>
                        setForm({ ...form, widthM: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Speed limit
                    <input
                      type="number"
                      value={form.speedLimitKmh}
                      onChange={(e) =>
                        setForm({ ...form, speedLimitKmh: e.target.value })
                      }
                    />
                  </label>
                </>
              )}
              {tab === "zoning-rules" && (
                <>
                  <label>
                    Zone (or use zone type)
                    <select
                      value={form.zoneId}
                      onChange={(e) =>
                        setForm({ ...form, zoneId: e.target.value })
                      }
                    >
                      <option value="">No specific zone</option>
                      {zones.map((x) => (
                        <option key={x.id} value={x.id}>
                          {x.zoneNumber}
                        </option>
                      ))}
                    </select>
                  </label>
                  {!form.zoneId && (
                    <label>
                      Zone type
                      <input
                        required
                        value={form.zoneType}
                        onChange={(e) =>
                          setForm({ ...form, zoneType: e.target.value })
                        }
                      />
                    </label>
                  )}
                  <label>
                    Rule key
                    <input
                      required
                      value={form.ruleKey}
                      onChange={(e) =>
                        setForm({ ...form, ruleKey: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Value
                    <input
                      required
                      type="number"
                      step="any"
                      value={form.ruleValue}
                      onChange={(e) =>
                        setForm({ ...form, ruleValue: e.target.value })
                      }
                    />
                  </label>
                  <label>
                    Unit
                    <input
                      required
                      value={form.unit}
                      onChange={(e) =>
                        setForm({ ...form, unit: e.target.value })
                      }
                    />
                  </label>
                </>
              )}
            </div>
            {tab !== "zoning-rules" && (
              <label>
                Geometry GeoJSON
                <textarea
                  required
                  value={form.geometry}
                  onChange={(e) =>
                    setForm({ ...form, geometry: e.target.value })
                  }
                />
              </label>
            )}
            <div className="actions">
              <button className="primary">
                {editing ? "Save changes" : "Create"}
              </button>
            </div>
          </form>
        )}
        <div style={{ marginTop: 16 }}>
          {!rows ? (
            <Spinner />
          ) : rows.length === 0 ? (
            <Empty
              title={`No ${tab.replaceAll("-", " ")}`}
              detail="No records are available in your authorized scope."
            />
          ) : (
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Record</th>
                    <th>Reference</th>
                    <th>Parent / type</th>
                    <th>State</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => {
                    const r = row as unknown as Record<string, unknown>;
                    return (
                      <tr key={String(r.id)}>
                        <td>{title(row)}</td>
                        <td>
                          <ShortId value={String(r.id)} />
                        </td>
                        <td>
                          {String(
                            r.zoneType ||
                              r.zoneId ||
                              r.blockId ||
                              r.roadType ||
                              "—",
                          )}
                        </td>
                        <td>
                          {r.isValidated === undefined
                            ? "Available"
                            : r.isValidated
                              ? "Validated"
                              : "Pending validation"}
                        </td>
                        <td>
                          <div className="actions">
                            <button
                              className="secondary"
                              onClick={() => void view(row)}
                            >
                              Details
                            </button>
                            {canManage &&
                              !(
                                tab === "parcels" &&
                                user?.role.name === "GISAnalyst"
                              ) && (
                                <button
                                  className="secondary"
                                  onClick={() => void edit(row)}
                                >
                                  <Edit3 size={13} />
                                  Edit
                                </button>
                              )}
                            {tab === "zoning-rules" &&
                              !r.isValidated &&
                              hasRole(user, ["Admin", "GISAnalyst"]) && (
                                <button
                                  className="secondary"
                                  onClick={() => void validate(String(r.id))}
                                >
                                  <CheckCircle2 size={13} />
                                  Validate
                                </button>
                              )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
        {selected && (
          <div style={{ marginTop: 16 }}>
            <h2>Selected record details</h2>
            <pre className="code">{JSON.stringify(selected, null, 2)}</pre>
          </div>
        )}
      </Card>
    </div>
  );
}
