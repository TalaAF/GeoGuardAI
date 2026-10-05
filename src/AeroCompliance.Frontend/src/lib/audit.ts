/**
 * Turns an audit log's raw before/after JSON snapshots into a human-readable "Field: old → new" list.
 * Never renders the snapshot as JSON — if it can't be safely interpreted as a flat object, the caller
 * falls back to showing only action/entity/actor/timestamp (see Audit.tsx).
 */

// Foreign-key-shaped fields (raw UUIDs) are dropped from the readable list entirely, rather than
// shown as a UUID — the audit table's own "Reference" column already carries a human reference
// where one can be derived (see deriveEntityReference below).
const ID_KEY = /(^id$|id$)/i;
// Defense in depth: never render these even if a future audit call accidentally included one.
const HIDDEN_KEY = /password|token|secret|credential|hash|apikey/i;

const UNIT_SUFFIXES: [RegExp, string][] = [
  [/M2$/, " m²"],
  [/Kmh$/, " km/h"],
  [/Cm$/, " cm"],
  [/Deg$/, "°"],
  [/Percent$/, "%"],
  [/M$/, " m"],
];

function splitUnit(key: string): { base: string; unit: string } {
  for (const [pattern, unit] of UNIT_SUFFIXES) {
    if (pattern.test(key)) return { base: key.replace(pattern, ""), unit };
  }
  return { base: key, unit: "" };
}

function prettyKey(key: string): string {
  const { base } = splitUnit(key);
  const spaced = base.replace(/([a-z0-9])([A-Z])/g, "$1 $2");
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

const ACCOUNT_STATE_KEYS = new Set(["isactive"]);

function prettyValue(key: string, value: unknown): string {
  if (value === null || value === undefined || value === "") return "—";
  if (typeof value === "boolean") {
    if (ACCOUNT_STATE_KEYS.has(key.toLowerCase())) return value ? "Active" : "Inactive";
    return value ? "Yes" : "No";
  }
  if (typeof value === "number") {
    const { unit } = splitUnit(key);
    return `${value}${unit}`;
  }
  if (typeof value === "string") {
    if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(value)) {
      try {
        return new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(
          new Date(value),
        );
      } catch {
        return value;
      }
    }
    return value.replaceAll("_", " ");
  }
  return String(value);
}

export interface AuditChange {
  label: string;
  before: string | null; // null = field did not exist before (a creation, or a newly-added field)
  after: string | null; // null = field no longer present after
}

function safeParseObject(json: string | null): Record<string, unknown> | null {
  if (!json) return {};
  try {
    const parsed = JSON.parse(json);
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) return parsed;
    return null;
  } catch {
    return null;
  }
}

/** Returns null when the snapshot can't be safely read as a flat object — the caller must then fall
 * back to a plain action/entity/actor/timestamp summary rather than showing raw text. */
export function diffAuditState(beforeJson: string | null, afterJson: string | null): AuditChange[] | null {
  const before = safeParseObject(beforeJson);
  const after = safeParseObject(afterJson);
  if (before === null || after === null) return null;
  const keys = new Set([...Object.keys(before), ...Object.keys(after)]);
  const changes: AuditChange[] = [];
  for (const key of keys) {
    if (ID_KEY.test(key) || HIDDEN_KEY.test(key)) continue;
    const hasBefore = Object.prototype.hasOwnProperty.call(before, key);
    const hasAfter = Object.prototype.hasOwnProperty.call(after, key);
    const b = before[key];
    const a = after[key];
    if (hasBefore && hasAfter && JSON.stringify(b) === JSON.stringify(a)) continue; // unchanged
    changes.push({
      label: prettyKey(key),
      before: hasBefore ? prettyValue(key, b) : null,
      after: hasAfter ? prettyValue(key, a) : null,
    });
  }
  return changes;
}

// A short list of fields, per entity type, that make a good human-readable reference — tried in
// order against whichever snapshot (after, then before) is available for this audit row.
const REFERENCE_FIELDS: Record<string, string[]> = {
  Zone: ["zoneNumber"],
  Block: ["blockNumber"],
  Parcel: ["parcelNumber"],
  Road: ["roadCode", "name"],
  ZoningRule: ["ruleKey"],
  Flight: ["missionName"],
  FlightMission: ["purpose"],
  Image: ["originalFileName"],
  User: ["fullName", "email"],
  Municipality: ["name"],
  Violation: ["violationType"],
};

/** Derives a human-readable reference for an audit row from its own before/after snapshot — no extra
 * API calls. Falls back to a short id-based reference (explicitly allowed as a support reference). */
export function deriveEntityReference(
  entityType: string,
  beforeJson: string | null,
  afterJson: string | null,
  entityId: string | null,
): string {
  const fields = REFERENCE_FIELDS[entityType] ?? [];
  for (const source of [safeParseObject(afterJson), safeParseObject(beforeJson)]) {
    if (!source) continue;
    for (const field of fields) {
      const value = source[field];
      if (typeof value === "string" && value.trim()) return value;
    }
  }
  return entityId ? `${entityType} ${entityId.slice(0, 8).toUpperCase()}` : entityType;
}
