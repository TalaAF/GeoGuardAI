export type Role =
  | "Admin"
  | "MunicipalityManager"
  | "Inspector"
  | "GISAnalyst"
  | "Reviewer"
  | "DroneOperator";
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
export interface CurrentUser {
  id: string;
  fullName: string;
  email: string;
  role: { id: number; name: Role };
  municipality: { id: string; name: string } | null;
  isActive: boolean;
}
export interface TokenPair {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
}
export interface StatusCount {
  status: string;
  count: number;
}
export interface ViolationSummary {
  id: string;
  violationType: string;
  status: string;
  aiConfidenceScore: number;
  flightId: string | null;
  flightMissionName?: string | null;
  createdAt: string;
  updatedAt: string;
  resultSource?: string | null;
  isApproximate?: boolean;
  isExperimental?: boolean;
  requiresManualReview?: boolean;
}
export interface ViolationHistory {
  status: string;
  changedBy: string | null;
  notes: string | null;
  changedAt: string;
}
export interface ViolationDetails extends ViolationSummary {
  georeferencingConfidence?: string | null;
  sourceGeoreferencingMethod?: string | null;
  detectionClassId?: number | null;
  measuredValue: number | null;
  requiredValue: number | null;
  exceedanceValue: number | null;
  exceedanceAreaM2: number | null;
  isZeroState: boolean;
  detectionId: string;
  detectionClass: string;
  detectionConfidence: number;
  flightMissionName?: string | null;
  sourceImageFileName?: string | null;
  parcelId: string | null;
  parcelNumber?: string | null;
  roadId: string | null;
  roadCode?: string | null;
  zoneId: string | null;
  zoneNumber?: string | null;
  blockId: string | null;
  blockNumber?: string | null;
  ruleId: string | null;
  ruleKey?: string | null;
  evidenceImageId: string | null;
  evidenceStoragePath: string | null;
  statusHistory: ViolationHistory[];
}
export interface DashboardSummary {
  totalFlights: number;
  images: number;
  pendingViolations: number;
  confirmedViolations: number;
  dismissedViolations: number;
  completedProcessingCount: number | null;
  recentViolations: ViolationSummary[];
  countsByStatus: StatusCount[];
  breakdown?: ResultBreakdown | null;
}

/** Splits headline counts by how each result was produced. */
export interface ResultBreakdown {
  realYoloAnalyses: number;
  mockAnalyses: number;
  geographicDetections: number;
  modelOnlyDetections: number;
  experimentalCandidates: number;
  mockCandidates: number;
  validatedCandidates: number;
  legacyUnclassifiedCandidates: number;
  humanConfirmedViolations: number;
  humanConfirmedExcludingSynthetic: number;
}
export interface FlightSummary {
  id: string;
  municipalityId: string;
  droneOperatorId: string;
  operatorName: string | null;
  missionName: string | null;
  status: string;
  scheduledAt: string | null;
  createdAt: string;
}
export interface FlightDetails extends FlightSummary {
  plannedArea: unknown | null;
  startedAt: string | null;
  endedAt: string | null;
  weatherConditions: string | null;
  qualityFlag: boolean;
  missionCount: number;
  imageCount: number;
  videoCount: number;
  orthomosaicCount: number;
}
export interface Mission {
  id: string;
  flightId: string;
  purpose: string | null;
  altitudeM: number | null;
  overlapPercent: number | null;
  notes: string | null;
}
/** Provenance of an extracted/typed value. See the root README's "Metadata precedence" section. */
export type MetadataSource = "GeoTiff" | "Exif" | "UserProvided" | "Missing";
export interface ImageInfo {
  id: string;
  flightId: string;
  originalFileName: string;
  contentType: string;
  byteLength: number;
  sha256Hash: string;
  capturedAt: string;
  latitude: number | null;
  longitude: number | null;
  altitudeM: number | null;
  cameraHeadingDeg: number | null;
  gpsAccuracyCm: number | null;
  processingStatus: string;
  attemptCount: number;
  uploadCompletedAt: string | null;
  createdAt: string;
  locationSource: MetadataSource;
  captureTimeSource: MetadataSource;
  metadataRequiresReview: boolean;
  sourceRasterCrs: string | null;
  rasterWidth: number | null;
  rasterHeight: number | null;
}
export interface ImageReceipt {
  imageId: string;
  flightId: string;
  processingStatus: string;
  originalFileName: string;
  byteLength: number;
  sha256Hash: string;
  statusUrl: string;
  effectiveCapturedAt: string;
  effectiveLatitude: number | null;
  effectiveLongitude: number | null;
  locationSource: MetadataSource;
  captureTimeSource: MetadataSource;
  metadataRequiresReview: boolean;
}
export interface ProcessingStatus {
  imageId: string;
  status: string;
  requestId: string | null;
  attemptCount: number;
  queuedAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  errorCode: string | null;
  errorMessage: string | null;
  analysis?: AnalysisSummary | null;
}

/** What one analysis produced. Model-only detections are real, but deliberately non-geographic. */
export interface AnalysisSummary {
  analysisId: string;
  requestId: string | null;
  mode: string;
  status: string;
  modelName: string | null;
  modelVersion: string | null;
  totalDetections: number;
  geographicDetections: number;
  modelOnlyDetections: number;
  outsideTrustedRoiCount: number;
  candidateViolationCount: number;
  geographicStatus: string;
  geographicEvaluationEligibility: string;
  geographicSkipReason: string | null;
  georeferencingMethod: string | null;
  georeferencingConfidence: string | null;
  trustedRoiRadiusM: number | null;
  startedAt: string | null;
  completedAt: string | null;
}
/** Provenance of a result's geometry, as emitted by the AI worker. */
export type ResultSource =
  | "RealYoloEstimatedGeometry"
  | "RealYoloGeoreferencedGeometry"
  | "RealYoloModelOnly"
  | "MockGeographicScenario"
  | "ImportedGisReference";

export interface Detection {
  id: string;
  imageId: string;
  imageAnalysisId: string | null;
  externalDetectionId?: string | null;
  classId: number | null;
  /** The AI model's own class name ("building", "road"). Serialized by the API as `className`. */
  className: string;
  confidence: number;
  resultSource?: ResultSource | string | null;
  isApproximate?: boolean;
  requiresManualReview?: boolean;
  georeferencingConfidence?: string | null;
  sourceGeoreferencingMethod?: string | null;
  distanceFromAnchorM?: number | null;
  analysisMode?: string | null;
  analysisRequestId?: string | null;
  georeferencingStatus?: string | null;
  trustedRoiRadiusM?: number | null;
  geometry: unknown;
  areaM2?: number | null;
  perimeterM?: number | null;
  heightM?: number | null;
  modelName?: string | null;
  modelVersion?: string | null;
  flightId?: string | null;
  sourceImageFileName?: string | null;
  flightMissionName?: string | null;
  createdAt: string;
}
export interface Municipality {
  id: string;
  name: string;
  region: string | null;
  mouSignedAt: string | null;
  apiAccessEnabled: boolean;
  isPilot: boolean;
  createdAt: string;
}
export interface UserInfo {
  id: string;
  fullName: string;
  email: string;
  role: { id: number; name: Role };
  municipality: { id: string; name: string } | null;
  isActive: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}
export interface AuditEntry {
  id: number;
  actorId: string | null;
  actorName: string | null;
  municipalityId: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  beforeState: string | null;
  afterState: string | null;
  ipAddress: string | null;
  createdAt: string;
}
export interface Zone {
  id: string;
  municipalityId: string;
  zoneNumber: string;
  name: string | null;
  zoneType: string | null;
  geometry: unknown;
  sourceDocument?: string | null;
  createdAt?: string;
}
export interface Block {
  id: string;
  zoneId: string;
  zoneNumber?: string | null;
  blockNumber: string;
  geometry: unknown;
  createdAt: string;
}
export interface Parcel {
  id: string;
  blockId: string;
  blockNumber?: string | null;
  zoneNumber?: string | null;
  parcelNumber: string;
  ownerName?: string | null;
  zoneType: string | null;
  areaM2: number | null;
  geometry: unknown;
  source?: string;
  createdAt?: string;
}
export interface Road {
  id: string;
  municipalityId: string;
  roadCode: string;
  name: string | null;
  roadType: string | null;
  widthM: number | null;
  speedLimitKmh: number | null;
  isSensitive: boolean;
  geometry: unknown;
  createdAt: string;
}
export interface ZoningRule {
  id: string;
  municipalityId: string;
  zoneId: string | null;
  zoneNumber?: string | null;
  zoneType: string | null;
  ruleKey: string;
  ruleValue: number;
  unit: string;
  sourceDocument: string | null;
  extractedByAi: boolean;
  isValidated: boolean;
  validatedBy: string | null;
  validatorName?: string | null;
  validatedAt: string | null;
  isActive: boolean;
  createdAt: string;
  // Phase 7: resolved server-side from the rule catalogue.
  ruleLabel?: string | null;
  ruleDescription?: string | null;
  enforcementState?: string | null;
  requirementDisplay?: string | null;
  isSupported?: boolean;
}

// --- Phase 6: analytics -----------------------------------------------------
export interface AnalyticsRange {
  from: string;
  to: string;
  days: number;
}
export interface AnalyticsSummary {
  totalFlights: number;
  completedFlights: number;
  imagesUploaded: number;
  imagesProcessed: number;
  imagesFailed: number;
  totalDetections: number;
  candidateViolations: number;
  confirmedViolations: number;
  dismissedViolations: number;
  averageConfidence: number | null;
  averageProcessingSeconds: number | null;
}
export interface AnalyticsDailyPoint {
  day: string;
  imagesUploaded: number;
  detections: number;
  candidateViolations: number;
  violationsConfirmed: number;
  violationsDismissed: number;
}
export interface AnalyticsCategory {
  key: string;
  label: string;
  count: number;
}
export interface AnalyticsRanking {
  reference: string;
  name: string | null;
  count: number;
}
export interface AnalyticsProcessing {
  averageSeconds: number | null;
  fastestSeconds: number | null;
  slowestSeconds: number | null;
  successRate: number | null;
  failedCount: number;
  measuredCount: number;
}
export interface AnalyticsOverview {
  range: AnalyticsRange;
  municipalityName: string | null;
  platformWide: boolean;
  summary: AnalyticsSummary;
  daily: AnalyticsDailyPoint[];
  violationsByStatus: AnalyticsCategory[];
  detectionsByClass: AnalyticsCategory[];
  violationsByType: AnalyticsCategory[];
  flightsByStatus: AnalyticsCategory[];
  imagesByStatus: AnalyticsCategory[];
  topRoads: AnalyticsRanking[];
  topZones: AnalyticsRanking[];
  topParcels: AnalyticsRanking[];
  processing: AnalyticsProcessing;
}

// --- Phase 6: reports -------------------------------------------------------
export interface ReportSummary {
  id: string;
  reference: string;
  violationReference: string;
  violationType: string;
  municipalityName: string;
  violationStatus: string;
  roadCode: string | null;
  roadName: string | null;
  parcelNumber: string | null;
  generatedAt: string;
  generatedBy: string;
  signedOff: boolean;
  signedOffByName: string | null;
  signedOffAt: string | null;
  downloadAvailable: boolean;
}
export interface ReportHistory {
  status: string;
  statusLabel: string;
  actorName: string | null;
  notes: string | null;
  changedAt: string;
}
export interface ReportDetails {
  id: string;
  reference: string;
  municipalityName: string;
  violationId: string;
  violationReference: string;
  violationType: string;
  violationStatus: string;
  violationStatusLabel: string;
  detectionClass: string;
  detectionConfidence: number;
  flightMissionName: string | null;
  sourceImageFileName: string | null;
  roadCode: string | null;
  roadName: string | null;
  parcelNumber: string | null;
  zoneNumber: string | null;
  blockNumber: string | null;
  evaluationType: string | null;
  measuredValue: number | null;
  requiredValue: number | null;
  exceedanceValue: number | null;
  exceedanceAreaM2: number | null;
  unit: string | null;
  reviewNotes: string | null;
  reviewedByName: string | null;
  reviewedAt: string | null;
  statusHistory: ReportHistory[];
  geometry: unknown;
  generatedAt: string;
  generatedBy: string;
  signedOff: boolean;
  signedOffByName: string | null;
  signedOffAt: string | null;
  downloadAvailable: boolean;
}

// --- Phase 7: zoning rule catalogue and compliance results ------------------
export interface RuleType {
  key: string;
  label: string;
  description: string;
  unit: string;
  unitLabel: string;
  minimumValue: number;
  maximumValue: number;
  comparisonLabel: string;
  calculationSummary: string;
  requiredContext: string;
  exampleValue: string;
}
export interface ComplianceEvaluation {
  id: string;
  detectionId: string;
  evaluationType: string;
  ruleLabel: string;
  ruleDescription: string | null;
  zoningRuleId: string | null;
  ruleEnforcementState: string | null;
  zoneNumber: string | null;
  zoneName: string | null;
  blockNumber: string | null;
  parcelNumber: string | null;
  roadCode: string | null;
  roadName: string | null;
  measuredValue: number | null;
  measuredDisplay: string | null;
  requiredValue: number | null;
  requiredDisplay: string | null;
  unit: string;
  outcome: string;
  outcomeLabel: string;
  explanation: string;
  metricCrs: number;
  violationId: string | null;
  evaluatedAt: string;
}
export interface ReEvaluationResult {
  detectionId: string;
  rulesEvaluated: number;
  passed: number;
  failed: number;
  notAssessed: number;
  violationsCreated: number;
  summary: string;
  evaluations: ComplianceEvaluation[];
}

// --- Phase 8: in-app notifications ------------------------------------------
export interface AppNotification {
  id: string;
  title: string;
  message: string;
  severity: "Information" | "Attention" | "Critical";
  severityLabel: string;
  isRead: boolean;
  createdAtUtc: string;
  route: string | null;
  relatedLabel: string | null;
  actionLabel: string;
}
export interface UnreadCount {
  unreadCount: number;
}
export interface MarkAllReadResult {
  updated: number;
}
export interface NotificationPreferences {
  candidateViolations: boolean;
  processingFailures: boolean;
  reviewOutcomes: boolean;
  generatedReports: boolean;
  lockedCategories: string[];
}
