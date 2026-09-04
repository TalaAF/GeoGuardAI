import type { CurrentUser, Role } from "../api/types";

/**
 * The single source of truth for what each role may see and do in the frontend.
 *
 * This table is a deliberate, hand-synchronized mirror of the backend's actual authorization
 * policies (`AeroCompliance.ApiService/Authorization/AuthorizationConfiguration.cs` and every
 * controller's `[Authorize]` attributes) — not a guess derived from page names. Each capability
 * below is commented with the exact backend policy or attribute it mirrors.
 *
 * SYNCHRONIZATION REQUIREMENT: whenever a backend policy or a controller's `[Authorize]` role
 * list changes, this table must be updated in the same change. The frontend never becomes more
 * permissive than the backend by construction (every capability check here is a subset check
 * against a role list copied from the backend), but it can go *stale* — i.e. still hide something
 * the backend has started allowing again, or still show a nav entry / action the backend has
 * started rejecting — if this file is not kept in step. There is no runtime mechanism that
 * detects that drift; it depends on the same PR that changes a backend policy also updating this
 * file. A server-provided `capabilities` field on `GET /users/me` would remove this manual sync
 * requirement, but was not implemented in this pass to avoid a backend DTO change; see the
 * `Immediate Fix` report for that tradeoff.
 *
 * The backend endpoint remains authoritative in every case: this table only decides what the
 * frontend *offers*, never what the API actually *permits*.
 */
export type Capability =
  // Dashboard — DashboardController is bare [Authorize]: any authenticated user.
  | "dashboard.view"
  // Violations — ViolationsController GET list/detail is bare [Authorize]: any authenticated user.
  | "violations.view"
  // Confirm/dismiss — Policies.ReviewViolations: Admin, Reviewer, Inspector.
  | "violations.review"
  // Generate a report from a violation — Policies.GenerateReports: Admin, Inspector, Reviewer.
  | "violations.generateReport"
  // Reviewer Work Queue — backed by the same bare-[Authorize] /api/violations endpoint, but
  // deliberately offered only to roles with a review/oversight remit (the same audience as
  // ViewAnalytics/ViewReports), matching "DroneOperator must not see reviewer workflow" below.
  | "reviewQueue.view"
  // Flights & Images (view) — FlightsController/FlightImagesController/ImagesController GET
  // endpoints are bare [Authorize]: any authenticated user.
  | "flights.view"
  // Create/edit flights, missions, status transitions, upload images, retry processing —
  // Policies.ManageFlights: Admin, MunicipalityManager, DroneOperator.
  | "flights.manage"
  // Pick which drone operator a flight belongs to, when creating/editing one. Not a distinct
  // backend policy — still gated by ManageFlights — but FlightsController assigns a DroneOperator
  // caller to themselves regardless of what they submit (`user.Role == DroneOperator ?
  // user.UserId : r.DroneOperatorId`), so the picker is meaningless for that role and is hidden
  // rather than shown-but-ignored. A strict subset of "flights.manage", the same way
  // "gis.parcels.edit" is a strict subset of "gis.manage".
  | "flights.chooseOperator"
  // AI Detections (view) — DetectionsController is bare [Authorize]: any authenticated user.
  | "detections.view"
  // Manual compliance re-evaluation — DetectionComplianceController's evaluate-compliance action
  // uses Policies.ManageGis: Admin, MunicipalityManager, GISAnalyst.
  | "detections.reevaluate"
  // GIS Records (view zones/blocks/parcels/roads/zoning rules) — bare [Authorize] on every GET.
  | "gis.view"
  // Create/edit zones/blocks/parcels/roads/zoning rules — Policies.ManageGis: Admin,
  // MunicipalityManager, GISAnalyst.
  | "gis.manage"
  // Edit an existing parcel specifically. NOTE: this is a deliberate product restriction, not a
  // backend one — ParcelsController's Update action is also gated by Policies.ManageGis, which
  // *does* include GISAnalyst. The frontend withholds parcel editing from GISAnalyst anyway
  // (cadastral/ownership data is kept to Admin and MunicipalityManager), so this capability is a
  // strict subset of "gis.manage": holding "gis.manage" does not imply holding this one, and a
  // denial here does not mean the backend would reject the same call. Documented explicitly so
  // the frontend-stricter-than-backend gap here is visible rather than silently baked into a
  // scattered comparison.
  | "gis.parcels.edit"
  // Validate a zoning rule — Policies.AnalyzeGis: Admin, GISAnalyst.
  | "gis.validateRule"
  // Administration: user management tab — Policies.ManageUsers: Admin, MunicipalityManager.
  | "administration.view"
  // Administration: municipality management tab — Policies.ManageMunicipalities: Admin only.
  | "administration.manageMunicipalities"
  // Audit Trail — AuditController is [Authorize(Roles = Admin + "," + MunicipalityManager)].
  | "audit.view"
  // Analytics — Policies.ViewAnalytics: Admin, MunicipalityManager, Inspector, Reviewer, GISAnalyst.
  | "analytics.view"
  // Choosing a municipality other than one's own in Analytics — only a platform Admin's token
  // satisfies AnalyticsController's IsPlatformAdmin check; every other role is rejected server-side.
  | "analytics.municipalityOverride"
  // Reports (view/list/download) — Policies.ViewReports: Admin, MunicipalityManager, Inspector,
  // Reviewer, GISAnalyst.
  | "reports.view"
  // Generate/regenerate a report — Policies.GenerateReports: Admin, Inspector, Reviewer.
  | "reports.generate"
  // Settings (the user's own profile) — no policy; any authenticated user reads their own data.
  | "settings.view"
  // Notification preferences (the user's own) — NotificationPreferencesController is bare
  // [Authorize], scoped server-side to the caller's own row.
  | "notificationPreferences.manage";

const ALL_ROLES: Role[] = [
  "Admin",
  "MunicipalityManager",
  "Inspector",
  "GISAnalyst",
  "Reviewer",
  "DroneOperator",
];

const CAPABILITY_ROLES: Record<Capability, Role[]> = {
  "dashboard.view": ALL_ROLES,
  "violations.view": ALL_ROLES,
  "violations.review": ["Admin", "Reviewer", "Inspector"],
  "violations.generateReport": ["Admin", "Inspector", "Reviewer"],
  "reviewQueue.view": [
    "Admin",
    "MunicipalityManager",
    "Inspector",
    "Reviewer",
    "GISAnalyst",
  ],
  "flights.view": ALL_ROLES,
  "flights.manage": ["Admin", "MunicipalityManager", "DroneOperator"],
  "flights.chooseOperator": ["Admin", "MunicipalityManager"],
  "detections.view": ALL_ROLES,
  "detections.reevaluate": ["Admin", "MunicipalityManager", "GISAnalyst"],
  "gis.view": ALL_ROLES,
  "gis.manage": ["Admin", "MunicipalityManager", "GISAnalyst"],
  "gis.parcels.edit": ["Admin", "MunicipalityManager"],
  "gis.validateRule": ["Admin", "GISAnalyst"],
  "administration.view": ["Admin", "MunicipalityManager"],
  "administration.manageMunicipalities": ["Admin"],
  "audit.view": ["Admin", "MunicipalityManager"],
  "analytics.view": [
    "Admin",
    "MunicipalityManager",
    "Inspector",
    "Reviewer",
    "GISAnalyst",
  ],
  "analytics.municipalityOverride": ["Admin"],
  "reports.view": [
    "Admin",
    "MunicipalityManager",
    "Inspector",
    "Reviewer",
    "GISAnalyst",
  ],
  "reports.generate": ["Admin", "Inspector", "Reviewer"],
  "settings.view": ALL_ROLES,
  "notificationPreferences.manage": ALL_ROLES,
};

/** Whether the given user holds a capability. False for a signed-out user (`null`). */
export function can(user: CurrentUser | null, capability: Capability): boolean {
  return !!user && CAPABILITY_ROLES[capability].includes(user.role.name);
}

// --- Named checks -----------------------------------------------------------
// Thin, readable wrappers over `can()` for call sites that want a verb, not a string literal.

export const canViewDashboard = (u: CurrentUser | null) => can(u, "dashboard.view");
export const canViewViolations = (u: CurrentUser | null) => can(u, "violations.view");
export const canReviewViolations = (u: CurrentUser | null) => can(u, "violations.review");
export const canGenerateViolationReport = (u: CurrentUser | null) =>
  can(u, "violations.generateReport");
export const canViewReviewQueue = (u: CurrentUser | null) => can(u, "reviewQueue.view");
export const canViewFlights = (u: CurrentUser | null) => can(u, "flights.view");
export const canOperateFlights = (u: CurrentUser | null) => can(u, "flights.manage");
export const canUploadImages = (u: CurrentUser | null) => can(u, "flights.manage");
export const canChooseFlightOperator = (u: CurrentUser | null) =>
  can(u, "flights.chooseOperator");
export const canViewDetections = (u: CurrentUser | null) => can(u, "detections.view");
export const canReevaluateCompliance = (u: CurrentUser | null) =>
  can(u, "detections.reevaluate");
export const canViewGis = (u: CurrentUser | null) => can(u, "gis.view");
export const canManageGis = (u: CurrentUser | null) => can(u, "gis.manage");
/** Stricter than `canManageGis` for parcels specifically — see the capability's own comment. */
export const canEditParcel = (u: CurrentUser | null) => can(u, "gis.parcels.edit");
export const canValidateZoningRule = (u: CurrentUser | null) => can(u, "gis.validateRule");
export const canViewAdministration = (u: CurrentUser | null) => can(u, "administration.view");
/** Same underlying policy as `administration.view` (Policies.ManageUsers) — exposed under this
 * name for call sites outside Administration itself that need to know whether a `/users` request
 * (e.g. populating a drone-operator picker) would be authorized. */
export const canManageUsers = canViewAdministration;
export const canManageMunicipalities = (u: CurrentUser | null) =>
  can(u, "administration.manageMunicipalities");
export const canViewAudit = (u: CurrentUser | null) => can(u, "audit.view");
export const canViewAnalytics = (u: CurrentUser | null) => can(u, "analytics.view");
export const canOverrideAnalyticsMunicipality = (u: CurrentUser | null) =>
  can(u, "analytics.municipalityOverride");
export const canViewReports = (u: CurrentUser | null) => can(u, "reports.view");
export const canGenerateReports = (u: CurrentUser | null) => can(u, "reports.generate");
export const canViewSettings = (u: CurrentUser | null) => can(u, "settings.view");
export const canManageNotificationPreferences = (u: CurrentUser | null) =>
  can(u, "notificationPreferences.manage");

/**
 * Whether the user is the platform-wide Admin (`ICurrentUserContext.IsPlatformAdmin` on the
 * backend: `Role == "Admin"`). Used for display niceties that mirror that specific backend flag
 * rather than a named policy — e.g. showing a cross-municipality column, or allowing a
 * municipality override query parameter — since a non-Admin's request for either is rejected
 * server-side regardless of what the frontend shows.
 */
export const isPlatformAdmin = (u: CurrentUser | null) => u?.role.name === "Admin";

/**
 * The first page a signed-in user can actually use, in priority order. `dashboard.view` is held
 * by every role, so this function always terminates — there is no role for which every branch is
 * denied, which is what rules out a redirect loop through the fallback itself.
 */
export function homeRouteFor(user: CurrentUser | null): string {
  if (canViewReviewQueue(user) && (user?.role.name === "Inspector" || user?.role.name === "Reviewer"))
    return "/review-queue";
  if (user?.role.name === "GISAnalyst" && canViewGis(user)) return "/gis";
  if (user?.role.name === "DroneOperator" && canViewFlights(user)) return "/flights";
  return "/dashboard";
}
