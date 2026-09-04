import { useCallback, useEffect, useState } from "react";
import { api, errorMessage } from "../api/client";
import type { NotificationPreferences } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { canManageNotificationPreferences } from "../auth/capabilities";
import { Card, Notice, PageTitle, ShortId, Spinner } from "../components/Common";

const CATEGORIES: {
  key: keyof Omit<NotificationPreferences, "lockedCategories">;
  lockKey: string;
  label: string;
  description: string;
}[] = [
  {
    key: "candidateViolations",
    lockKey: "CandidateViolations",
    label: "New candidate violations",
    description: "A new AI-detected violation is ready for review.",
  },
  {
    key: "processingFailures",
    lockKey: "ProcessingFailures",
    label: "Processing failures",
    description: "An image failed to process and needs attention.",
  },
  {
    key: "reviewOutcomes",
    lockKey: "ReviewOutcomes",
    label: "Review outcomes",
    description: "A violation you have an interest in was confirmed or dismissed.",
  },
  {
    key: "generatedReports",
    lockKey: "GeneratedReports",
    label: "Generated reports",
    description: "A report you requested has finished generating.",
  },
];

/** The user's own in-app notification category switches — never another user's. */
function NotificationPreferencesPanel() {
  const [prefs, setPrefs] = useState<NotificationPreferences | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const { data } = await api.get<NotificationPreferences>("/notification-preferences");
      setPrefs(data);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const toggle = async (key: (typeof CATEGORIES)[number]["key"]) => {
    if (!prefs || saving) return;
    const next = { ...prefs, [key]: !prefs[key] };
    setPrefs(next);
    setSaving(key);
    setError("");
    try {
      const { data } = await api.put<NotificationPreferences>("/notification-preferences", {
        candidateViolations: next.candidateViolations,
        processingFailures: next.processingFailures,
        reviewOutcomes: next.reviewOutcomes,
        generatedReports: next.generatedReports,
      });
      setPrefs(data);
    } catch (e) {
      setPrefs(prefs); // roll back the optimistic flip
      setError(errorMessage(e));
    } finally {
      setSaving(null);
    }
  };

  return (
    <Card>
      <h2>Notification preferences</h2>
      <p className="chart-note">
        Choose which in-app notification categories you want to receive. These apply only to your
        own account.
      </p>
      {error && <Notice kind="error">{error}</Notice>}
      {loading ? (
        <Spinner />
      ) : prefs ? (
        <div className="pref-list">
          {CATEGORIES.map((c) => {
            const locked = prefs.lockedCategories.includes(c.lockKey);
            return (
              <label key={c.key} className="pref-row">
                <div>
                  <strong>{c.label}</strong>
                  <small>{c.description}</small>
                  {locked && (
                    <small className="pref-locked">
                      Always on for your role — this keeps operational failures visible to the
                      people who can act on them.
                    </small>
                  )}
                </div>
                <input
                  type="checkbox"
                  checked={prefs[c.key]}
                  disabled={locked || saving === c.key}
                  aria-label={`${c.label}${locked ? " (always on)" : ""}`}
                  onChange={() => void toggle(c.key)}
                />
              </label>
            );
          })}
        </div>
      ) : null}
    </Card>
  );
}

export default function SettingsPage() {
  const { user } = useAuth();
  return (
    <div className="page">
      <PageTitle
        title="Settings"
        description="Current authenticated profile and environment-safe display preferences."
      />
      <Card>
        <h2>Current profile</h2>
        <div className="detail-grid">
          <div className="detail">
            <small>User</small>
            <strong>{user?.fullName}</strong>
          </div>
          <div className="detail">
            <small>Email</small>
            <strong>{user?.email}</strong>
          </div>
          <div className="detail">
            <small>Role</small>
            <strong>{user?.role.name}</strong>
          </div>
          <div className="detail">
            <small>Municipality</small>
            <strong>{user?.municipality?.name || "Platform-wide"}</strong>
          </div>
          <div className="detail">
            <small>User ID</small>
            <ShortId value={user?.id} />
          </div>
          <div className="detail">
            <small>Session storage</small>
            <strong>Current browser tab only</strong>
          </div>
        </div>
      </Card>
      {canManageNotificationPreferences(user) && <NotificationPreferencesPanel />}
    </div>
  );
}
