import {
  AlertTriangle,
  BarChart3,
  ClipboardCheck,
  FileQuestion,
  Gauge,
  Layers3,
  LogOut,
  Map,
  Plane,
  Radar,
  ScrollText,
  Settings,
  Shield,
  Users,
} from "lucide-react";
import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import type { Capability } from "../auth/capabilities";
import { can } from "../auth/capabilities";
import NotificationBell from "./NotificationBell";

/**
 * One entry per navigable area, each keyed to the exact capability its route is gated by. Filtered
 * with the same `can()` check `RequireCapability` uses for routing, so the sidebar can never show
 * a link the router would refuse — the two can't disagree because they share one source.
 */
const nav: { to: string; label: string; icon: typeof Gauge; capability: Capability }[] = [
  { to: "/dashboard", label: "Dashboard", icon: Gauge, capability: "dashboard.view" },
  { to: "/violations", label: "Violations", icon: AlertTriangle, capability: "violations.view" },
  {
    to: "/review-queue",
    label: "Review Queue",
    icon: ClipboardCheck,
    capability: "reviewQueue.view",
  },
  { to: "/flights", label: "Flights & Images", icon: Plane, capability: "flights.view" },
  { to: "/detections", label: "AI Detections", icon: Radar, capability: "detections.view" },
  { to: "/gis", label: "GIS Records", icon: Map, capability: "gis.view" },
  {
    to: "/administration",
    label: "Administration",
    icon: Users,
    capability: "administration.view",
  },
  { to: "/audit", label: "Audit Trail", icon: ScrollText, capability: "audit.view" },
  { to: "/analytics", label: "Analytics", icon: BarChart3, capability: "analytics.view" },
  { to: "/reports", label: "Reports", icon: FileQuestion, capability: "reports.view" },
  { to: "/settings", label: "Settings", icon: Settings, capability: "settings.view" },
];

export default function Shell() {
  const { user, logout } = useAuth();

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <span>
            <Shield size={17} />
          </span>
          <div>
            AeroCompliance<small>Municipal Monitoring</small>
          </div>
        </div>
        <nav>
          {nav
            .filter((x) => can(user, x.capability))
            .map(({ to, label, icon: Icon }) => (
              <NavLink key={to} to={to}>
                <Icon size={17} />
                {label}
              </NavLink>
            ))}
        </nav>
        <div className="sidebar-foot">
          <Layers3 size={15} /> Real-time compliance
        </div>
      </aside>
      <div className="workspace">
        <header>
          <div>
            <strong>
              {user?.municipality?.name || "All municipalities (platform-wide access)"}
            </strong>
            <small>Connected to AeroCompliance</small>
          </div>
          <div className="profile">
            <NotificationBell />
            <span className="avatar">
              {user?.fullName
                .split(/\s+/)
                .map((x) => x[0])
                .slice(0, 2)
                .join("")}
            </span>
            <div>
              <strong>{user?.fullName}</strong>
              <small>{user?.role.name}</small>
            </div>
            <button
              className="icon-button"
              title="Logout"
              onClick={() => void logout()}
            >
              <LogOut size={16} />
            </button>
          </div>
        </header>
        <main>
          <Outlet />
        </main>
      </div>
    </div>
  );
}
