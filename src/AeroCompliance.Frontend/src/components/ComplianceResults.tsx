import { CheckCircle2, HelpCircle, RefreshCw, XCircle } from "lucide-react";
import type { ComplianceEvaluation } from "../api/types";
import { fmtDate } from "./Common";

/** Maps an outcome code to its badge styling and icon. */
function outcomeVisual(outcome: string) {
  if (outcome === "pass") return { cls: "pass", Icon: CheckCircle2 };
  if (outcome === "fail" || outcome === "candidate_violation")
    return { cls: "fail", Icon: XCircle };
  return { cls: "unknown", Icon: HelpCircle };
}

/**
 * Badge for a zoning rule's enforcement state. Anything other than "Enforced" means the rule
 * cannot currently produce a violation, so it is styled as a warning rather than a neutral tag.
 */
export function EnforcementBadge({ state }: { state?: string | null }) {
  if (!state) return null;
  const key = state.toLowerCase().replace(/\s+/g, "-");
  return <span className={`enforcement enforcement-${key}`}>{state}</span>;
}

/** One compliance result: rule, location, measured vs. required, and a plain-language verdict. */
function EvaluationCard({ item }: { item: ComplianceEvaluation }) {
  const { cls, Icon } = outcomeVisual(item.outcome);
  const location = item.roadCode
    ? item.roadName
      ? `${item.roadCode} — ${item.roadName}`
      : item.roadCode
    : item.parcelNumber
      ? `Parcel ${item.parcelNumber}`
      : item.zoneNumber
        ? `Zone ${item.zoneNumber}`
        : null;

  return (
    <article className={`compliance-card ${cls}`}>
      <header>
        <div>
          <strong>{item.ruleLabel}</strong>
          {location && <small>{location}</small>}
        </div>
        <span className={`compliance-outcome ${cls}`}>
          <Icon size={14} />
          {item.outcomeLabel}
        </span>
      </header>

      {(item.measuredDisplay || item.requiredDisplay) && (
        <div className="compliance-figures">
          <div>
            <small>Measured</small>
            <strong>{item.measuredDisplay ?? "Not measured"}</strong>
          </div>
          <div>
            <small>Required limit</small>
            <strong>{item.requiredDisplay ?? "Not recorded"}</strong>
          </div>
        </div>
      )}

      <p className="compliance-explanation">{item.explanation}</p>

      <footer>
        <span>Assessed {fmtDate(item.evaluatedAt)}</span>
        <EnforcementBadge state={item.ruleEnforcementState} />
      </footer>
    </article>
  );
}

/**
 * The Compliance Results section shared by the detection and violation pages.
 * Shows nothing technical: no rule keys, no ids, no geometry.
 */
export default function ComplianceResults({
  items,
  loading,
  canReevaluate,
  onReevaluate,
  busy,
  message,
}: {
  items: ComplianceEvaluation[];
  loading?: boolean;
  canReevaluate?: boolean;
  onReevaluate?: () => void;
  busy?: boolean;
  message?: string;
}) {
  return (
    <div className="compliance-section">
      <div className="compliance-header">
        <div>
          <h2>Compliance results</h2>
          <p className="chart-note">
            Each validated zoning rule that applies to this detection, measured against the
            municipality's map data.
          </p>
        </div>
        {canReevaluate && onReevaluate && (
          <button
            type="button"
            className="secondary"
            disabled={busy}
            onClick={onReevaluate}
          >
            <RefreshCw size={14} />
            {busy ? "Re-checking…" : "Re-evaluate compliance"}
          </button>
        )}
      </div>

      {message && <p className="compliance-summary">{message}</p>}

      {loading ? (
        <p className="muted-note">Loading compliance results…</p>
      ) : items.length === 0 ? (
        <p className="muted-note">
          No zoning rule has been applied to this detection yet. A rule must be a supported type,
          active, validated, and cover this detection's zone before it is checked.
        </p>
      ) : (
        <div className="compliance-grid">
          {items.map((x) => (
            <EvaluationCard key={x.id} item={x} />
          ))}
        </div>
      )}
    </div>
  );
}
