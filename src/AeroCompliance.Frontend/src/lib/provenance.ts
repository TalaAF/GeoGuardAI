/**
 * How a result's location was obtained, expressed once so every page says the same thing.
 *
 * The badge is driven by the structured `resultSource` the AI worker records. Filenames are used
 * only as a last resort for development rows written before provenance existed, and such rows are
 * labelled "legacy" so nobody mistakes a guess for a recorded fact.
 */

export type ProvenanceTone = "real" | "estimated" | "modelOnly" | "mock" | "reference" | "unknown";

export interface ProvenanceBadge {
  label: string;
  tone: ProvenanceTone;
  /** True when the position is an estimate and must never be treated as authoritative. */
  approximate: boolean;
  title: string;
  legacy: boolean;
}

const BADGES: Record<string, Omit<ProvenanceBadge, "legacy">> = {
  RealYoloEstimatedGeometry: {
    label: "Real YOLO — Estimated location",
    tone: "estimated",
    approximate: true,
    title:
      "Real model output placed with an estimated georeference. Experimental: not valid for cadastral, surveying, enforcement or legal use.",
  },
  RealYoloGeoreferencedGeometry: {
    label: "Real YOLO — Validated location",
    tone: "real",
    approximate: false,
    title: "Real model output placed with the raster's own surveyed georeference.",
  },
  RealYoloModelOnly: {
    label: "Real YOLO — Model only",
    tone: "modelOnly",
    approximate: false,
    title:
      "A real model prediction with no trustworthy map position. It is reported but never placed on the map.",
  },
  MockGeographicScenario: {
    label: "Mock scenario",
    tone: "mock",
    approximate: false,
    title: "Synthetic development data from a deterministic scenario. Not a real detection.",
  },
  ImportedGisReference: {
    label: "Imported GIS reference",
    tone: "reference",
    approximate: false,
    title: "A feature imported from municipal or reference GIS data, not produced by the model.",
  },
};

/**
 * Resolve a badge for a result. `fileName` is consulted only when no structured source exists.
 */
export function provenanceBadge(
  resultSource: string | null | undefined,
  options: { fileName?: string | null; mode?: string | null } = {},
): ProvenanceBadge {
  const known = resultSource ? BADGES[resultSource] : undefined;
  if (known) return { ...known, legacy: false };

  // No recorded provenance: this row predates provenance tracking. Infer only enough to be useful,
  // and say plainly that it was inferred.
  const mode = (options.mode ?? "").toLowerCase();
  if (mode === "mock") {
    return {
      label: "Mock scenario (legacy)",
      tone: "mock",
      approximate: false,
      title:
        "No provenance was recorded for this row; it ran in mock mode, so it is synthetic development data.",
      legacy: true,
    };
  }
  if (mode === "yolo") {
    return {
      label: "Real YOLO (legacy)",
      tone: "real",
      approximate: false,
      title: "No provenance was recorded for this row; it ran in yolo mode.",
      legacy: true,
    };
  }
  return {
    label: "Source not recorded",
    tone: "unknown",
    approximate: false,
    title:
      "This row predates provenance tracking, so how its location was obtained is not known. Treat it as development data.",
    legacy: true,
  };
}

/** The AI model's own class, kept distinct from any review/legal classification. */
export function aiClassLabel(className: string | null | undefined, classId?: number | null): string {
  const name = (className ?? "").trim();
  if (!name) return classId != null ? `Class ${classId}` : "Not reported";
  return classId != null ? `${name} (${classId})` : name;
}

export const APPROXIMATE_WARNING =
  "This location was estimated from a single OSM anchor, rotation, and approximate scale. " +
  "It is experimental and is not suitable for cadastral, surveying, enforcement, or legal use.";

export const MOCK_WARNING =
  "Deterministic development scenario — synthetic data generated for testing. " +
  "It does not represent a real detection or a real site condition.";
