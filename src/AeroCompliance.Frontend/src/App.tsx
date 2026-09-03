import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import { AuthProvider, hasRole, useAuth } from "./auth/AuthContext";
import type { Role } from "./api/types";
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
import SettingsPage from "./pages/Settings";
import Unavailable from "./pages/Unavailable";

function Protected({ roles }: { roles?: Role[] }) {
  const { user, loading } = useAuth();
  const location = useLocation();
  if (loading) return <div className="splash">Loading secure session…</div>;
  if (!user)
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  if (roles && !hasRole(user, roles)) return <Unavailable access />;
  return <Shell />;
}
function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<Protected />}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<Dashboard />} />
        <Route path="violations" element={<Violations />} />
        <Route path="violations/:id" element={<ViolationDetail />} />
        <Route path="flights" element={<Flights />} />
        <Route path="flights/:id" element={<FlightDetail />} />
        <Route path="detections" element={<Detections />} />
        <Route path="gis" element={<Gis />} />
        <Route path="analytics" element={<Unavailable feature="Analytics" />} />
        <Route path="reports" element={<Unavailable feature="Reports" />} />
        <Route path="settings" element={<SettingsPage />} />
        <Route path="*" element={<Unavailable notFound />} />
      </Route>
      <Route element={<Protected roles={["Admin", "MunicipalityManager"]} />}>
        {" "}
        <Route path="administration" element={<Administration />} />
        <Route path="audit" element={<Audit />} />
      </Route>
    </Routes>
  );
}
export default function App() {
  return (
    <AuthProvider>
      <AppRoutes />
    </AuthProvider>
  );
}
