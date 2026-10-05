import type { ReactNode } from "react";
import { provenanceBadge } from "../lib/provenance";

export function PageTitle({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <div className="page-title">
      <div>
        <h1>{title}</h1>
        {description && <p>{description}</p>}
      </div>
      <div className="actions">{actions}</div>
    </div>
  );
}
export function Card({
  children,
  className = "",
}: {
  children: ReactNode;
  className?: string;
}) {
  return <section className={`card ${className}`}>{children}</section>;
}
export function Notice({
  kind = "info",
  children,
}: {
  kind?: "info" | "error" | "success";
  children: ReactNode;
}) {
  return <div className={`notice ${kind}`}>{children}</div>;
}
export function Empty({ title, detail }: { title: string; detail: string }) {
  return (
    <div className="empty">
      <strong>{title}</strong>
      <span>{detail}</span>
    </div>
  );
}
export function Spinner() {
  return (
    <div className="spinner-wrap">
      <span className="spinner" /> Loading…
    </div>
  );
}
export function ShortId({ value }: { value?: string | null }) {
  return (
    <span className="mono" title={value || undefined}>
      {value ? value.slice(0, 8).toUpperCase() : "—"}
    </span>
  );
}
export function Status({ value }: { value: string }) {
  return (
    <span className={`status status-${value}`}>
      {value.replaceAll("_", " ")}
    </span>
  );
}
/**
 * Where a result's location came from. Rendered from recorded provenance; a "legacy" badge means
 * the row predates provenance tracking and the label was inferred, not read.
 */
export function SourceBadge({
  resultSource,
  mode,
  fileName,
}: {
  resultSource?: string | null;
  mode?: string | null;
  fileName?: string | null;
}) {
  const badge = provenanceBadge(resultSource, { mode, fileName });
  return (
    <span className={`source-badge source-${badge.tone}`} title={badge.title}>
      {badge.label}
    </span>
  );
}

export function Pager({
  page,
  totalPages,
  onChange,
}: {
  page: number;
  totalPages: number;
  onChange(p: number): void;
}) {
  return (
    <div className="pager">
      <button disabled={page <= 1} onClick={() => onChange(page - 1)}>
        Previous
      </button>
      <span>
        Page {page} of {Math.max(1, totalPages)}
      </span>
      <button disabled={page >= totalPages} onClick={() => onChange(page + 1)}>
        Next
      </button>
    </div>
  );
}
export const fmtDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: "medium",
        timeStyle: "short",
      }).format(new Date(value))
    : "—";
export const fmtPercent = (value: number) => `${Math.round(value * 100)}%`;
