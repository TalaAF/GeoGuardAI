import { useEffect } from "react";
import { Navigate, Outlet, Route, Routes, useLocation } from "react-router-dom";
import { AuthProvider, useAuth } from "./auth/AuthContext";
import { homeRouteFor, type Capability, can } from "./auth/capabilities";
import AccessDeniedToast, { announceAccessDenied } from "./components/AccessDeniedToast";
import Shell from "./components/Shell";
import Login from "./pages/Login";
import Dashboard from "./pages/Dashboard";
import Violations from "./pages/Violations";
import ViolationDetail from "./pages/ViolationDetail";
import Flights from "./pages/Flights";
import FlightDetail from "./pages/FlightDetail";
import Detections from "./pages/Detections";
import Administration from "./pages/Administration";
import Gis from "./pages/Gis";
import Audit from "./pages/Audit";
import Analytics from "./pages/Analytics";
import Reports from "./pages/Reports";
import ReportDetail from "./pages/ReportDetail";
import ReviewQueue from "./pages/ReviewQueue";
import SettingsPage from "./pages/Settings";
import Unavailable from "./pages/Unavailable";

/**
 * The single authenticated layout. Mounted exactly once for the whole signed-in app — every
 * capability-gated route below is a *child* of this route via nested `<Outlet/>`, so switching
 * between e.g. Dashboard and Analytics never remounts the sidebar/header/notification bell.
 *
 * While the session is being resolved (token verified, current user fetched), nothing about the
 * authenticated UI is rendered — not the sidebar, not a role-specific link — so there is no
 * moment where an Admin-only nav item can flash before disappearing for another role.
 */
function AuthGate() {
  const { user, loading } = useAuth();
  const location = useLocation();
  if (loading) return <div className="splash">Loading secure session…</div>;
  if (!user)
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  return <Shell />;
}

/**
 * Gates one route subtree behind a single capability. Used as a route's `element`, wrapping the
 * actual page route(s) as children so the guard runs — and the page's own data-fetching effects
 * do not start — before anything under it is rendered.
 *
 * An unauthorized direct visit (typed URL, refresh, stale link) never renders the page or issues
 * its API calls: this component returns a redirect immediately, so the child route's `<Outlet/>`
 * is never reached. The destination is `homeRouteFor(user)`, which is guaranteed to be a page the
 * same user can open — that guarantee is what rules out a redirect loop.
 */
function RequireCapability({ capability }: { capability: Capability }) {
  const { user } = useAuth(); // AuthGate above already resolved loading and the signed-in user.
  const location = useLocation();

  useEffect(() => {
    if (user && !can(user, capability)) announceAccessDenied();
    // Re-announce only when the guarded route or the signed-in identity actually changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user?.id, capability, location.pathname]);

  if (!user || !can(user, capability))
    return <Navigate to={homeRouteFor(user)} replace />;
  return <Outlet />;
}

function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />

      <Route element={<AuthGate />}>
        <Route index element={<Navigate to="/dashboard" replace />} />

        <Route element={<RequireCapability capability="dashboard.view" />}>
          <Route path="dashboard" element={<Dashboard />} />
        </Route>

        <Route element={<RequireCapability capability="violations.view" />}>
          <Route path="violations" element={<Violations />} />
          <Route path="violations/:id" element={<ViolationDetail />} />
        </Route>

        <Route element={<RequireCapability capability="reviewQueue.view" />}>
          <Route path="review-queue" element={<ReviewQueue />} />
        </Route>

        <Route element={<RequireCapability capability="flights.view" />}>
          <Route path="flights" element={<Flights />} />
          <Route path="flights/:id" element={<FlightDetail />} />
        </Route>

        <Route element={<RequireCapability capability="detections.view" />}>
          <Route path="detections" element={<Detections />} />
        </Route>

        <Route element={<RequireCapability capability="gis.view" />}>
          <Route path="gis" element={<Gis />} />
        </Route>

        <Route element={<RequireCapability capability="analytics.view" />}>
          <Route path="analytics" element={<Analytics />} />
        </Route>

        <Route element={<RequireCapability capability="reports.view" />}>
          <Route path="reports" element={<Reports />} />
          <Route path="reports/:id" element={<ReportDetail />} />
        </Route>

        <Route element={<RequireCapability capability="administration.view" />}>
          <Route path="administration" element={<Administration />} />
        </Route>

        <Route element={<RequireCapability capability="audit.view" />}>
          <Route path="audit" element={<Audit />} />
        </Route>

        <Route element={<RequireCapability capability="settings.view" />}>
          <Route path="settings" element={<SettingsPage />} />
        </Route>

        <Route path="*" element={<Unavailable notFound />} />
      </Route>
    </Routes>
  );
}
export default function App() {
  return (
    <AuthProvider>
      {/* Mounted once, above the routed area, so a redirect-in-progress never unmounts it. */}
      <AccessDeniedToast />
      <AppRoutes />
    </AuthProvider>
  );
}
