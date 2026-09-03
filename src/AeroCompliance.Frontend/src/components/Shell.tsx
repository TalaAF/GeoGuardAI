import {
  AlertTriangle,
  BarChart3,
  FileQuestion,
  Gauge,
  GitBranch,
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
import { hasRole, useAuth } from "../auth/AuthContext";
import type { Role } from "../api/types";

const nav: { to: string; label: string; icon: typeof Gauge; roles?: Role[] }[] =
  [
    { to: "/dashboard", label: "Dashboard", icon: Gauge },
    { to: "/violations", label: "Violations", icon: AlertTriangle },
    { to: "/flights", label: "Flights & Images", icon: Plane },
    { to: "/detections", label: "AI Detections", icon: Radar },
    { to: "/gis", label: "GIS Records", icon: Map },
    {
      to: "/administration",
      label: "Administration",
      icon: Users,
      roles: ["Admin", "MunicipalityManager"],
    },
    {
      to: "/audit",
      label: "Audit Trail",
      icon: ScrollText,
      roles: ["Admin", "MunicipalityManager"],
    },
    { to: "/analytics", label: "Analytics", icon: BarChart3 },
    { to: "/reports", label: "Reports", icon: FileQuestion },
    { to: "/settings", label: "Settings", icon: Settings },
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
            .filter((x) => !x.roles || hasRole(user, x.roles))
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
              {user?.municipality?.name || "Platform administration"}
            </strong>
            <small>Connected to AeroCompliance API</small>
          </div>
          <div className="profile">
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
