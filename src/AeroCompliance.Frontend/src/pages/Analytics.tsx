import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { RefreshCw } from "lucide-react";
import { api, errorMessage } from "../api/client";
import type {
  AnalyticsCategory,
  AnalyticsOverview,
  AnalyticsRanking,
  Municipality,
} from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { Card, Empty, Notice, PageTitle, Spinner } from "../components/Common";

type PresetKey = "7" | "30" | "90" | "custom";

const PRESETS: { key: PresetKey; label: string }[] = [
  { key: "7", label: "Last 7 days" },
  { key: "30", label: "Last 30 days" },
  { key: "90", label: "Last 90 days" },
  { key: "custom", label: "Custom range" },
];

// Chart palette, kept in step with the status colours used elsewhere in the app.
const SERIES = {
  images: "#2563eb",
  detections: "#7c3aed",
  candidates: "#d97706",
  confirmed: "#dc2626",
  dismissed: "#059669",
};
const CATEGORY_COLORS = [
  "#2563eb",
  "#dc2626",
  "#059669",
  "#d97706",
  "#7c3aed",
  "#0891b2",
  "#be185d",
];

const isoDay = (d: Date) => d.toISOString().slice(0, 10);
const daysAgo = (n: number) => {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - n);
  return d;
};

/** Short axis label for a yyyy-mm-dd day, e.g. "4 Sep". */
const dayTick = (value: string) =>
  new Intl.DateTimeFormat(undefined, { day: "numeric", month: "short" }).format(
    new Date(`${value}T00:00:00Z`),
  );

const fullDay = (value: string) =>
  new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(
    new Date(`${value}T00:00:00Z`),
  );

function duration(seconds: number | null) {
  if (seconds == null) return "Not available";
  if (seconds < 90) return `${seconds.toFixed(seconds < 10 ? 1 : 0)} s`;
  return `${(seconds / 60).toFixed(1)} min`;
}

export default function Analytics() {
  const { user } = useAuth();
  const isAdmin = user?.role.name === "Admin";
  const [preset, setPreset] = useState<PresetKey>("30");
  const [customFrom, setCustomFrom] = useState(isoDay(daysAgo(29)));
  const [customTo, setCustomTo] = useState(isoDay(new Date()));
  const [municipalityId, setMunicipalityId] = useState("");
  const [municipalities, setMunicipalities] = useState<Municipality[]>([]);
  const [data, setData] = useState<AnalyticsOverview | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const range = useMemo(() => {
    if (preset === "custom") return { from: customFrom, to: customTo };
    const span = Number(preset);
    return { from: isoDay(daysAgo(span - 1)), to: isoDay(new Date()) };
  }, [preset, customFrom, customTo]);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const { data } = await api.get<AnalyticsOverview>("/analytics/overview", {
        params: {
          from: `${range.from}T00:00:00Z`,
          to: `${range.to}T23:59:59Z`,
          municipalityId: municipalityId || undefined,
        },
      });
      setData(data);
    } catch (e) {
      setData(null);
      setError(errorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [range.from, range.to, municipalityId]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!isAdmin) return;
    api
      .get<{ items: Municipality[] }>("/municipalities", {
        params: { page: 1, pageSize: 100 },
      })
      .then(({ data }) => setMunicipalities(data.items))
      .catch(() => setMunicipalities([]));
  }, [isAdmin]);

  const summary = data?.summary;
  // "No activity" means nothing happened in the window, not that the request failed.
  const empty =
    !!summary &&
    summary.totalFlights === 0 &&
    summary.imagesUploaded === 0 &&
    summary.totalDetections === 0 &&
    summary.candidateViolations +
      summary.confirmedViolations +
      summary.dismissedViolations ===
      0;

  return (
    <div className="page">
      <PageTitle
        title="Analytics"
        description={
          data
            ? `${fullDay(range.from)} – ${fullDay(range.to)} · ${
                data.platformWide
                  ? "All municipalities"
                  : (data.municipalityName ?? "Your municipality")
              }`
            : "Operational activity for the selected period"
        }
        actions={
          <button
            type="button"
            className="secondary"
            onClick={() => void load()}
            disabled={loading}
          >
            <RefreshCw size={14} /> Refresh
          </button>
        }
      />

      <Card className="analytics-filters">
        <div className="filter-row">
          <div
            className="preset-group"
            role="group"
            aria-label="Reporting period"
          >
            {PRESETS.map((p) => (
              <button
                key={p.key}
                type="button"
                aria-pressed={preset === p.key}
                className={preset === p.key ? "preset active" : "preset"}
                onClick={() => setPreset(p.key)}
              >
                {p.label}
              </button>
            ))}
          </div>
          {preset === "custom" && (
            <div className="field-row two custom-range">
              <label>
                From
                <input
                  type="date"
                  value={customFrom}
                  max={customTo}
                  onChange={(e) => setCustomFrom(e.target.value)}
                />
              </label>
              <label>
                To
                <input
                  type="date"
                  value={customTo}
                  min={customFrom}
                  onChange={(e) => setCustomTo(e.target.value)}
                />
              </label>
            </div>
          )}
          {isAdmin && (
            <label className="municipality-picker">
              Municipality
              <select
                value={municipalityId}
                onChange={(e) => setMunicipalityId(e.target.value)}
              >
                <option value="">All municipalities</option>
                {municipalities.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.name}
                  </option>
                ))}
              </select>
            </label>
          )}
        </div>
      </Card>

      {loading && (
        <Card>
          <div className="spinner-wrap">
            <span className="spinner" /> Loading analytics…
          </div>
        </Card>
      )}

      {!loading && error && (
        <Card>
          <Notice kind="error">Unable to load analytics.</Notice>
          <p className="muted-note">{error}</p>
          <button type="button" className="secondary" onClick={() => void load()}>
            Try again
          </button>
        </Card>
      )}

      {!loading && !error && data && empty && (
        <Card>
          <div className="empty">
            <Empty
              title="No activity was recorded for the selected period."
              detail="Widen the date range, or review the flights and images that have been uploaded."
            />
            <div className="actions">
              <Link className="primary" to="/flights">
                View flights
              </Link>
              <button
                type="button"
                className="secondary"
                onClick={() => setPreset("90")}
              >
                Try the last 90 days
              </button>
            </div>
          </div>
        </Card>
      )}

      {!loading && !error && data && !empty && summary && (
        <>
          <div className="metric-grid">
            <Metric
              label="Flights"
              value={summary.totalFlights}
              note={`${summary.completedFlights} completed`}
            />
            <Metric
              label="Processed images"
              value={summary.imagesProcessed}
              note={`of ${summary.imagesUploaded} uploaded`}
            />
            <Metric
              label="AI detections"
              value={summary.totalDetections}
              note={
                summary.averageConfidence == null
                  ? "No confidence recorded"
                  : `${Math.round(summary.averageConfidence * 100)}% average confidence`
              }
            />
            <Metric
              label="Candidate violations"
              value={summary.candidateViolations}
              note="Awaiting human review"
            />
            <Metric
              label="Confirmed violations"
              value={summary.confirmedViolations}
              note={`${summary.dismissedViolations} dismissed`}
            />
            <Metric
              label="Failed processing"
              value={summary.imagesFailed}
              note={
                data.processing.successRate == null
                  ? "No images finished processing yet"
                  : `${Math.round(data.processing.successRate * 100)}% succeeded`
              }
              tone={summary.imagesFailed > 0 ? "warn" : undefined}
            />
          </div>

          <Card>
            <h2 className="chart-title">Activity over time</h2>
            <p className="chart-note">
              Daily counts across the selected period. Days with no activity are
              shown as zero.
            </p>
            <ResponsiveContainer width="100%" height={280}>
              <LineChart
                data={data.daily}
                margin={{ top: 8, right: 12, left: -18, bottom: 0 }}
              >
                <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                <XAxis
                  dataKey="day"
                  tickFormatter={dayTick}
                  fontSize={11}
                  minTickGap={22}
                />
                <YAxis allowDecimals={false} fontSize={11} />
                <Tooltip
                  labelFormatter={(v) => fullDay(String(v))}
                  contentStyle={{ fontSize: 12 }}
                />
                <Legend wrapperStyle={{ fontSize: 12 }} />
                <Line
                  type="monotone"
                  dataKey="imagesUploaded"
                  name="Images uploaded"
                  stroke={SERIES.images}
                  strokeWidth={2}
                  dot={false}
                />
                <Line
                  type="monotone"
                  dataKey="detections"
                  name="AI detections"
                  stroke={SERIES.detections}
                  strokeWidth={2}
                  dot={false}
                />
                <Line
                  type="monotone"
                  dataKey="candidateViolations"
                  name="Candidate violations"
                  stroke={SERIES.candidates}
                  strokeWidth={2}
                  dot={false}
                />
                <Line
                  type="monotone"
                  dataKey="violationsConfirmed"
                  name="Confirmed"
                  stroke={SERIES.confirmed}
                  strokeWidth={2}
                  dot={false}
                />
                <Line
                  type="monotone"
                  dataKey="violationsDismissed"
                  name="Dismissed"
                  stroke={SERIES.dismissed}
                  strokeWidth={2}
                  dot={false}
                />
              </LineChart>
            </ResponsiveContainer>
          </Card>

          <div className="chart-grid">
            <CategoryPie
              title="Violations by status"
              note="Current status of violations raised in this period."
              data={data.violationsByStatus}
            />
            <CategoryBars
              title="Detections by class"
              note="What the AI model identified."
              data={data.detectionsByClass}
            />
            <CategoryBars
              title="Image processing health"
              note="Processing state of images uploaded in this period."
              data={data.imagesByStatus}
              footer={
                <dl className="mini-facts">
                  <div>
                    <dt>Average duration</dt>
                    <dd>{duration(data.processing.averageSeconds)}</dd>
                  </div>
                  <div>
                    <dt>Fastest</dt>
                    <dd>{duration(data.processing.fastestSeconds)}</dd>
                  </div>
                  <div>
                    <dt>Slowest</dt>
                    <dd>{duration(data.processing.slowestSeconds)}</dd>
                  </div>
                  <div>
                    <dt>Measured from</dt>
                    <dd>
                      {data.processing.measuredCount === 0
                        ? "No timed images"
                        : `${data.processing.measuredCount} image${
                            data.processing.measuredCount === 1 ? "" : "s"
                          }`}
                    </dd>
                  </div>
                </dl>
              }
            />
            <CategoryPie
              title="Violations by type"
              note="The compliance rule each violation was raised against."
              data={data.violationsByType}
            />
          </div>

          <Card>
            <h2 className="chart-title">Most affected locations</h2>
            <p className="chart-note">
              The five roads, zones and parcels with the most violations in this
              period.
            </p>
            <div className="ranking-grid">
              <Ranking title="Roads" rows={data.topRoads} />
              <Ranking title="Zones" rows={data.topZones} />
              <Ranking title="Parcels" rows={data.topParcels} />
            </div>
          </Card>
        </>
      )}
    </div>
  );
}

function Metric({
  label,
  value,
  note,
  tone,
}: {
  label: string;
  value: number;
  note: string;
  tone?: "warn";
}) {
  return (
    <div className={`metric-card${tone ? ` ${tone}` : ""}`}>
      <span className="metric-label">{label}</span>
      <strong className="metric-value">{value.toLocaleString()}</strong>
      <span className="metric-note">{note}</span>
    </div>
  );
}

function CategoryPie({
  title,
  note,
  data,
}: {
  title: string;
  note: string;
  data: AnalyticsCategory[];
}) {
  return (
    <Card>
      <h2 className="chart-title">{title}</h2>
      <p className="chart-note">{note}</p>
      {data.length === 0 ? (
        <p className="muted-note">Nothing was recorded in this period.</p>
      ) : (
        <ResponsiveContainer width="100%" height={240}>
          <PieChart>
            <Pie
              data={data}
              dataKey="count"
              nameKey="label"
              innerRadius={48}
              outerRadius={82}
              paddingAngle={2}
            >
              {data.map((entry, i) => (
                <Cell
                  key={entry.key}
                  fill={CATEGORY_COLORS[i % CATEGORY_COLORS.length]}
                />
              ))}
            </Pie>
            <Tooltip contentStyle={{ fontSize: 12 }} />
            <Legend wrapperStyle={{ fontSize: 12 }} />
          </PieChart>
        </ResponsiveContainer>
      )}
    </Card>
  );
}

function CategoryBars({
  title,
  note,
  data,
  footer,
}: {
  title: string;
  note: string;
  data: AnalyticsCategory[];
  footer?: React.ReactNode;
}) {
  return (
    <Card>
      <h2 className="chart-title">{title}</h2>
      <p className="chart-note">{note}</p>
      {data.length === 0 ? (
        <p className="muted-note">Nothing was recorded in this period.</p>
      ) : (
        <ResponsiveContainer width="100%" height={240}>
          <BarChart
            data={data}
            layout="vertical"
            margin={{ top: 4, right: 16, left: 8, bottom: 4 }}
          >
            <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" horizontal={false} />
            <XAxis type="number" allowDecimals={false} fontSize={11} />
            <YAxis
              type="category"
              dataKey="label"
              width={130}
              fontSize={11}
              tickLine={false}
            />
            <Tooltip cursor={{ fill: "#f1f5f9" }} contentStyle={{ fontSize: 12 }} />
            <Bar dataKey="count" name="Count" radius={[0, 4, 4, 0]}>
              {data.map((entry, i) => (
                <Cell
                  key={entry.key}
                  fill={CATEGORY_COLORS[i % CATEGORY_COLORS.length]}
                />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      )}
      {footer}
    </Card>
  );
}

function Ranking({ title, rows }: { title: string; rows: AnalyticsRanking[] }) {
  return (
    <div className="ranking">
      <h3>{title}</h3>
      {rows.length === 0 ? (
        <p className="muted-note">No violations were linked to a {title.slice(0, -1).toLowerCase()}.</p>
      ) : (
        <ol>
          {rows.map((r) => (
            <li key={r.reference}>
              <span>
                <strong>{r.reference}</strong>
                {r.name && <small>{r.name}</small>}
              </span>
              <span className="ranking-count">{r.count}</span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
