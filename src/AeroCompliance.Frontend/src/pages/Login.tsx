import { useState, type FormEvent } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { Shield, LogIn } from "lucide-react";
import { errorMessage } from "../api/client";
import { useAuth } from "../auth/AuthContext";

export default function Login() {
  const { user, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  if (user) return <Navigate to="/dashboard" replace />;
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      await login(email, password);
      navigate((location.state as { from?: string })?.from || "/dashboard", {
        replace: true,
      });
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="login-page">
      <section className="login-visual">
        <div className="login-copy">
          <span className="shield">
            <Shield />
          </span>
          <p className="eyebrow">Municipal intelligence platform</p>
          <h1>
            See change.
            <br />
            Verify impact.
            <br />
            Act with evidence.
          </h1>
          <p>
            Secure aerial compliance monitoring backed by geospatial analysis
            and an auditable review workflow.
          </p>
        </div>
      </section>
      <section className="login-panel">
        <form className="login-card" onSubmit={submit}>
          <div className="login-logo">
            <Shield size={19} /> AeroCompliance
          </div>
          <h2>Welcome back</h2>
          <p>Sign in with your municipal or platform account.</p>
          {error && <div className="notice error">{error}</div>}
          <label>
            Email
            <input
              type="email"
              autoComplete="username"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoFocus
            />
          </label>
          <label>
            Password
            <input
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </label>
          <button className="primary large" disabled={busy}>
            <LogIn size={16} />
            {busy ? "Signing in…" : "Sign in"}
          </button>
          <small className="hardening">
            Tokens are stored for this browser tab. HttpOnly refresh cookies are
            a future production hardening item.
          </small>
        </form>
      </section>
    </div>
  );
}
