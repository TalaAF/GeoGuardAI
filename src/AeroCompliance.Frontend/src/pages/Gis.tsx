import { CheckCircle2, Edit3, Plus, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from "react";
import { api, errorMessage, page as getPage } from "../api/client";
import type {
  Block,
  Municipality,
  Parcel,
  Road,
  RuleType,
  Zone,
  ZoningRule,
} from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { canEditParcel, canManageGis, canManageMunicipalities, canValidateZoningRule } from "../auth/capabilities";
import {
  Card,
  Empty,
  Notice,
  PageTitle,
  Spinner,
  fmtDate,
} from "../components/Common";
import GeometryEditor from "../components/GeometryEditor";
import GeoMap from "../components/GeoMap";
import { EnforcementBadge } from "../components/ComplianceResults";
import { formatArea, totalAreaM2, type NormalizedMultiPolygon } from "../lib/geometry";

type Tab = "zones" | "blocks" | "parcels" | "roads" | "zoning-rules";
type Row = Zone | Block | Parcel | Road | ZoningRule;
const GEOMETRY_TABS: Tab[] = ["zones", "blocks", "parcels", "roads"];
const BOUNDARY_LABEL: Record<Tab, string> = {
  zones: "zone boundary",
  blocks: "block boundary",
  parcels: "parcel boundary",
  roads: "road coverage area",
  "zoning-rules": "boundary",
};
function title(row: Row): string {
  const r = row as unknown as Record<string, unknown>;
  return String(
    r.zoneNumber || r.blockNumber || r.parcelNumber || r.roadCode || r.ruleKey || r.id,
  );
}
function safeAreaM2(geometry: unknown): number | null {
  try {
    const g = geometry as { type?: string; coordinates?: unknown } | null;
    if (!g || g.type !== "MultiPolygon" || !Array.isArray(g.coordinates)) return null;
    return totalAreaM2(g as NormalizedMultiPolygon);
  } catch {
    return null;
  }
}
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
  geometry: null as NormalizedMultiPolygon | null,
};
export default function Gis() {
  const { user } = useAuth();
  const [tab, setTab] = useState<Tab>("zones");
  const [rows, setRows] = useState<Row[] | null>(null);
  const [selected, setSelected] = useState<Row | null>(null);
  const [zones, setZones] = useState<Zone[]>([]);
  const [blocks, setBlocks] = useState<Block[]>([]);
  const [municipalities, setMunicipalities] = useState<Municipality[]>([]);
  const [ruleTypes, setRuleTypes] = useState<RuleType[]>([]);
  // The guided rule form is driven entirely by the backend catalogue, so the unit, limits and
  // help text can never drift from what the engine actually enforces.
  useEffect(() => {
    api
      .get<RuleType[]>("/zoning-rules/types")
      .then(({ data }) => setRuleTypes(data))
      .catch(() => setRuleTypes([]));
  }, []);
  const [form, setForm] = useState({
    ...initial,
    municipalityId: user?.municipality?.id || "",
  });
  const selectedRuleType = ruleTypes.find((t) => t.key === form.ruleKey) ?? null;
  const [editing, setEditing] = useState<string | null>(null);
  const [initialGeometry, setInitialGeometry] = useState<unknown>(null);
  const [show, setShow] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const canManage = canManageGis(user);
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
      if (canManageMunicipalities(user))
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
    setInitialGeometry(null);
    setForm({ ...initial, municipalityId: user?.municipality?.id || "" });
  };
  const save = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    if (GEOMETRY_TABS.includes(tab) && !form.geometry) {
      setError("Draw, import, or paste a valid boundary before saving.");
      return;
    }
    try {
      let body: Record<string, unknown>;
      if (tab === "zones")
        body = {
          municipalityId: form.municipalityId || undefined,
          zoneNumber: form.zoneNumber,
          name: form.name || null,
          zoneType: form.zoneType || null,
          geometry: form.geometry,
          sourceDocument: form.sourceDocument || null,
        };
      else if (tab === "blocks")
        body = {
          zoneId: form.zoneId,
          blockNumber: form.blockNumber,
          geometry: form.geometry,
        };
      else if (tab === "parcels")
        body = {
          blockId: form.blockId,
          parcelNumber: form.parcelNumber,
          ownerName: form.ownerName || null,
          zoneType: form.zoneType || null,
          areaM2: form.areaM2 ? Number(form.areaM2) : null,
          geometry: form.geometry,
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
          geometry: form.geometry,
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
      setError(errorMessage(e));
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
      geometry: null,
    });
    setInitialGeometry(r.geometry ?? null);
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
  const parentBoundary =
    tab === "blocks" && form.zoneId
      ? (() => {
          const zone = zones.find((z) => z.id === form.zoneId);
          return zone ? { geometry: zone.geometry, label: `Zone ${zone.zoneNumber}` } : null;
        })()
      : tab === "parcels" && form.blockId
        ? (() => {
            const block = blocks.find((b) => b.id === form.blockId);
            return block ? { geometry: block.geometry, label: `Block ${block.blockNumber}` } : null;
          })()
        : null;
  return (
    <div className="page">
      <PageTitle
        title="GIS Records"
        description="Zones, blocks, parcels, roads, and zoning rules for your municipality. Draw boundaries on the map or import a boundary file — the underlying map data is generated automatically."
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
              {canManageMunicipalities(user) &&
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
                  <label className="span-2">
                    Rule type
                    <select
                      required
                      value={form.ruleKey}
                      onChange={(e) => {
                        const picked = ruleTypes.find((t) => t.key === e.target.value);
                        setForm({
                          ...form,
                          ruleKey: e.target.value,
                          // The unit is a property of the rule type, never typed by hand.
                          unit: picked?.unit ?? "",
                        });
                      }}
                    >
                      <option value="">Select a rule type…</option>
                      {ruleTypes.map((t) => (
                        <option key={t.key} value={t.key}>
                          {t.label}
                        </option>
                      ))}
                    </select>
                    {selectedRuleType && (
                      <small className="field-help">{selectedRuleType.description}</small>
                    )}
                  </label>
                  <label>
                    {selectedRuleType
                      ? `${selectedRuleType.comparisonLabel} (${selectedRuleType.unitLabel})`
                      : "Required value"}
                    <input
                      required
                      type="number"
                      step="any"
                      min={selectedRuleType?.minimumValue}
                      max={selectedRuleType?.maximumValue}
                      disabled={!selectedRuleType}
                      placeholder={selectedRuleType?.exampleValue}
                      value={form.ruleValue}
                      onChange={(e) => setForm({ ...form, ruleValue: e.target.value })}
                    />
                    {selectedRuleType && (
                      <small className="field-help">
                        Between {selectedRuleType.minimumValue} and{" "}
                        {selectedRuleType.maximumValue} {selectedRuleType.unitLabel}.
                      </small>
                    )}
                  </label>
                  <label>
                    Unit
                    <input
                      readOnly
                      value={selectedRuleType?.unitLabel ?? "Select a rule type first"}
                    />
                    <small className="field-help">Set automatically by the rule type.</small>
                  </label>
                  <label>
                    Applies to zone
                    <select
                      value={form.zoneId}
                      onChange={(e) => setForm({ ...form, zoneId: e.target.value })}
                    >
                      <option value="">All zones of a type (below)</option>
                      {zones.map((x) => (
                        <option key={x.id} value={x.id}>
                          {x.zoneNumber}
                          {x.name ? ` — ${x.name}` : ""}
                        </option>
                      ))}
                    </select>
                    <small className="field-help">
                      A rule for a specific zone takes precedence over a zone-type rule.
                    </small>
                  </label>
                  {!form.zoneId && (
                    <label>
                      Zone type
                      <input
                        required
                        placeholder="residential"
                        value={form.zoneType}
                        onChange={(e) => setForm({ ...form, zoneType: e.target.value })}
                      />
                    </label>
                  )}
                  {selectedRuleType && (
                    <p className="rule-preview span-2">
                      <strong>{selectedRuleType.label}:</strong>{" "}
                      {form.ruleValue || selectedRuleType.exampleValue}{" "}
                      {selectedRuleType.unitLabel}
                      {" · "}
                      {selectedRuleType.requiredContext}
                    </p>
                  )}
                  <p className="field-help span-2">
                    A new or edited rule always returns to <em>awaiting validation</em> and does not
                    affect compliance until an authorised user validates it.
                  </p>
                </>
              )}
            </div>
            {GEOMETRY_TABS.includes(tab) && (
              <label>
                {tab === "roads" ? "Road coverage area" : "Boundary"}
                <GeometryEditor
                  key={`${tab}-${editing ?? "new"}`}
                  initialGeometry={initialGeometry}
                  onChange={(g) => setForm((f) => ({ ...f, geometry: g }))}
                  boundaryLabel={BOUNDARY_LABEL[tab]}
                  parentBoundary={parentBoundary}
                />
                {tab === "roads" && (
                  <p style={{ fontSize: 11, color: "#94a3b8", margin: "4px 0 0" }}>
                    This is the road's coverage/boundary area, not a centerline.
                  </p>
                )}
              </label>
            )}
            <div className="actions">
              <button
                className="primary"
                disabled={GEOMETRY_TABS.includes(tab) && !form.geometry}
              >
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
                  <tr>{columnHeaders(tab).map((h) => <th key={h}>{h}</th>)}<th /></tr>
                </thead>
                <tbody>
                  {rows.map((row) => {
                    const r = row as unknown as Record<string, unknown>;
                    return (
                      <tr key={String(r.id)}>
                        {columnCells(tab, row).map((cell, i) => (
                          <td key={i}>{cell == null ? "—" : String(cell)}</td>
                        ))}
                        <td>
                          <div className="actions">
                            <button
                              className="secondary"
                              onClick={() => void view(row)}
                            >
                              Details
                            </button>
                            {(tab === "parcels" ? canEditParcel(user) : canManage) && (
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
                              canValidateZoningRule(user) && (
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
            <h2>{title(selected)}</h2>
            <RecordDetail tab={tab} row={selected} zones={zones} blocks={blocks} onClose={() => setSelected(null)} />
          </div>
        )}
      </Card>
    </div>
  );
}

function columnHeaders(tab: Tab): string[] {
  switch (tab) {
    case "zones": return ["Zone number", "Name", "Type", "State"];
    case "blocks": return ["Block number", "Zone", "Area", "State"];
    case "parcels": return ["Parcel number", "Block", "Zone type", "Area", "State"];
    case "roads": return ["Road code", "Name", "Type", "Width", "Sensitive"];
    default: return ["Rule", "Target", "Value", "State"];
  }
}
function columnCells(tab: Tab, row: Row) {
  const r = row as unknown as Record<string, unknown>;
  const state = r.isValidated === undefined ? "Available" : r.isValidated ? "Validated" : "Pending validation";
  switch (tab) {
    case "zones":
      return [r.zoneNumber, r.name || "—", r.zoneType || "—", state];
    case "blocks": {
      const area = safeAreaM2(r.geometry);
      return [r.blockNumber, r.zoneNumber ? `Zone ${r.zoneNumber}` : "—", area != null ? formatArea(area) : "—", state];
    }
    case "parcels": {
      const area = typeof r.areaM2 === "number" ? r.areaM2 : safeAreaM2(r.geometry);
      return [r.parcelNumber, r.blockNumber ? `Block ${r.blockNumber}` : "—", r.zoneType || "—", area != null ? formatArea(area) : "—", state];
    }
    case "roads":
      return [r.roadCode, r.name || "—", r.roadType || "—", r.widthM != null ? `${r.widthM} m` : "—", r.isSensitive ? "Yes" : "No"];
    default:
      return [
        r.ruleLabel || String(r.ruleKey).replace(/_/g, " "),
        r.zoneNumber ? `Zone ${r.zoneNumber}` : r.zoneType ? `Zone type: ${r.zoneType}` : "—",
        r.requirementDisplay || `${r.ruleValue} ${r.unit}`,
        r.enforcementState || state,
      ];
  }
}

/** Per-type, human-readable record detail — never a raw JSON dump. Shows the boundary on a map with
 * its parent (Zone/Block) as a reference layer when one is known. */
function RecordDetail({
  tab,
  row,
  zones,
  blocks,
  onClose,
}: {
  tab: Tab;
  row: Row;
  zones: Zone[];
  blocks: Block[];
  onClose: () => void;
}) {
  const r = row as unknown as Record<string, unknown>;
  const geometry = r.geometry ?? null;
  const parentBoundary =
    tab === "blocks" && r.zoneId
      ? (() => {
          const zone = zones.find((z) => z.id === r.zoneId);
          return zone ? { geometry: zone.geometry, label: `Zone ${zone.zoneNumber}` } : null;
        })()
      : tab === "parcels" && r.blockId
        ? (() => {
            const block = blocks.find((b) => b.id === r.blockId);
            return block ? { geometry: block.geometry, label: `Block ${block.blockNumber}` } : null;
          })()
        : null;
  const rows: [string, ReactNode][] =
    tab === "zones"
      ? [
          ["Zone number", String(r.zoneNumber)],
          ["Name", String(r.name || "—")],
          ["Zone type", String(r.zoneType || "—")],
          ["Source document", String(r.sourceDocument || "—")],
          ["Approximate area", geometry ? formatArea(safeAreaM2(geometry) ?? 0) : "—"],
          ["Created", fmtDate(r.createdAt as string)],
        ]
      : tab === "blocks"
        ? [
            ["Block number", String(r.blockNumber)],
            ["Zone", r.zoneNumber ? `Zone ${r.zoneNumber}` : "—"],
            ["Approximate area", geometry ? formatArea(safeAreaM2(geometry) ?? 0) : "—"],
            ["Created", fmtDate(r.createdAt as string)],
          ]
        : tab === "parcels"
          ? [
              ["Parcel number", String(r.parcelNumber)],
              ["Block", r.blockNumber ? `Block ${r.blockNumber}` : "—"],
              ["Zone", r.zoneNumber ? `Zone ${r.zoneNumber}` : "—"],
              ["Zone type", String(r.zoneType || "—")],
              ["Area", r.areaM2 != null ? formatArea(Number(r.areaM2)) : geometry ? formatArea(safeAreaM2(geometry) ?? 0) : "—"],
              ["Source", String(r.source || "—")],
              ["Owner", r.ownerName ? String(r.ownerName) : "Restricted to authorized roles"],
            ]
          : tab === "roads"
            ? [
                ["Road code", String(r.roadCode)],
                ["Name", String(r.name || "—")],
                ["Type", String(r.roadType || "—")],
                ["Width", r.widthM != null ? `${r.widthM} m` : "—"],
                ["Speed limit", r.speedLimitKmh != null ? `${r.speedLimitKmh} km/h` : "—"],
                ["Sensitive", r.isSensitive ? "Yes" : "No"],
              ]
            : [
                ["Rule", String(r.ruleLabel || String(r.ruleKey).replace(/_/g, " "))],
                ["Requirement", String(r.requirementDisplay || `${r.ruleValue} ${r.unit}`)],
                ["Applies to", r.zoneNumber ? `Zone ${r.zoneNumber}` : r.zoneType ? `Zone type: ${r.zoneType}` : "—"],
                ["Enforcement", String(r.enforcementState || (r.isActive ? "Active" : "Inactive"))],
                ["AI-extracted", r.extractedByAi ? "Yes" : "No"],
                ["Human-validated", r.isValidated ? "Yes" : "No"],
                ["Validated by", r.validatorName ? String(r.validatorName) : "—"],
              ];
  return (
    <div className="grid two-col">
      {tab === "zoning-rules" && (
        <div className="span-2">
          <EnforcementBadge state={String(r.enforcementState || "")} />
          {r.enforcementState !== "Enforced" && (
            <Notice kind="error">
              {r.enforcementState === "Awaiting validation"
                ? "This rule does not affect compliance yet. An authorised user must validate it before it can produce a violation."
                : r.enforcementState === "Inactive"
                  ? "This rule is switched off and is not applied to any detection."
                  : r.enforcementState === "Unsupported"
                    ? "This rule type cannot be enforced automatically. It is kept for reference only and is never used to assess a detection."
                    : "This rule's value or unit is outside the range this rule type accepts, so it is not applied."}
            </Notice>
          )}
          {r.extractedByAi === true && r.isValidated !== true && (
            <Notice>
              This rule was extracted automatically from a source document. AI-extracted rules are
              never applied to compliance until a person has checked and validated them.
            </Notice>
          )}
        </div>
      )}
      <div className="detail-grid">
        {rows.map(([label, value]) => (
          <div className="detail" key={label}>
            <small>{label}</small>
            <strong>{value}</strong>
          </div>
        ))}
        <div className="actions" style={{ marginTop: 10 }}>
          <button className="secondary" onClick={onClose}>Close</button>
        </div>
      </div>
      {tab !== "zoning-rules" && geometry != null && (
        <GeoMap
          features={[{ id: String(r.id), geometry, kind: "parcel", label: title(row) }]}
          parentFeature={parentBoundary}
          legend
        />
      )}
    </div>
  );
}
