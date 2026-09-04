import { Bell, Check, CheckCheck, X } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../api/client";
import type { AppNotification, Paged, UnreadCount } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { fmtDate } from "./Common";

const POLL_MS = 25_000;

/**
 * Patterns for routes the SPA actually has today. Matched in addition to the relative/no-open-
 * -redirect check below: the backend's route catalog is free to describe a route the frontend
 * hasn't built yet (kept there for forward compatibility), and rather than link to a dead page —
 * which would still read as "broken" even though it's same-origin and safe — the action button is
 * simply omitted for a notification whose target doesn't match a page that exists.
 */
const KNOWN_ROUTES = [
  /^\/violations\/[0-9a-f-]+$/i,
  /^\/reports\/[0-9a-f-]+$/i,
  /^\/flights\/[0-9a-f-]+$/i,
  /^\/detections$/i,
  /^\/gis$/i,
  /^\/review-queue$/i,
  /^\/dashboard$/i,
];

/**
 * Whether a notification's route is both safe to navigate to (relative, same-origin, not an open
 * redirect) and points at a page this build of the SPA can actually render. Route-level
 * authorization for the destination is still enforced by the router's own capability guard when
 * the user arrives — this only decides whether to offer the button at all.
 */
function isKnownRoute(route: string | null): route is string {
  if (!route || !route.startsWith("/") || route.startsWith("//")) return false;
  return KNOWN_ROUTES.some((pattern) => pattern.test(route));
}

/**
 * Header notification bell: unread badge, dropdown panel, and the read/dismiss actions.
 *
 * Polls the unread count on a timer rather than opening a socket. Poll failures are swallowed —
 * the badge simply stops updating — so a backend blip can never spam the user or break the header.
 */
export default function NotificationBell() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [unread, setUnread] = useState(0);
  const [items, setItems] = useState<AppNotification[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  // Keyed on the user id so a different sign-in never inherits the previous user's cached data.
  const userId = user?.id ?? null;

  useEffect(() => {
    setUnread(0);
    setItems([]);
    setOpen(false);
    setError("");
  }, [userId]);

  // Poll the badge. Stops entirely once signed out, so no request is made after logout.
  useEffect(() => {
    if (!userId) return;
    let cancelled = false;
    const tick = async () => {
      try {
        const { data } = await api.get<UnreadCount>("/notifications/unread-count");
        if (!cancelled) setUnread(data.unreadCount);
      } catch {
        // Silent by design: a failed poll must not raise a toast every 25 seconds.
      }
    };
    void tick();
    const timer = window.setInterval(tick, POLL_MS);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [userId]);

  const load = useCallback(
    async (onlyUnread: boolean) => {
      setLoading(true);
      setError("");
      try {
        const { data } = await api.get<Paged<AppNotification>>("/notifications", {
          params: { page: 1, pageSize: 15, unreadOnly: onlyUnread || undefined },
        });
        setItems(data.items);
      } catch {
        setItems([]);
        setError("Unable to load notifications. Try again.");
      } finally {
        setLoading(false);
      }
    },
    [],
  );

  useEffect(() => {
    if (open) void load(unreadOnly);
  }, [open, unreadOnly, load]);

  // Close on outside click or Escape, and return focus to the trigger.
  useEffect(() => {
    if (!open) return;
    const onPointer = (e: MouseEvent) => {
      if (
        !panelRef.current?.contains(e.target as Node) &&
        !buttonRef.current?.contains(e.target as Node)
      ) {
        setOpen(false);
      }
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setOpen(false);
        buttonRef.current?.focus();
      }
    };
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const markRead = async (n: AppNotification) => {
    if (n.isRead) return;
    setItems((xs) => xs.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
    setUnread((c) => Math.max(0, c - 1));
    try {
      await api.post(`/notifications/${n.id}/read`);
    } catch {
      setError("That notification could not be marked as read.");
    }
  };

  const openNotification = async (n: AppNotification) => {
    await markRead(n);
    setOpen(false);
    if (isKnownRoute(n.route)) navigate(n.route);
  };

  const markAllRead = async () => {
    if (busy) return;
    setBusy("all");
    try {
      await api.post("/notifications/read-all");
      setItems((xs) => xs.map((x) => ({ ...x, isRead: true })));
      setUnread(0);
      if (unreadOnly) await load(true);
    } catch {
      setError("Notifications could not be marked as read.");
    } finally {
      setBusy(null);
    }
  };

  const dismiss = async (n: AppNotification) => {
    if (busy) return;
    setBusy(n.id);
    try {
      await api.delete(`/notifications/${n.id}`);
      setItems((xs) => xs.filter((x) => x.id !== n.id));
      if (!n.isRead) setUnread((c) => Math.max(0, c - 1));
    } catch {
      setError("That notification could not be dismissed.");
    } finally {
      setBusy(null);
    }
  };

  if (!userId) return null;

  const badge = unread > 99 ? "99+" : String(unread);

  return (
    <div className="bell-wrap">
      <button
        ref={buttonRef}
        type="button"
        className="icon-button bell-button"
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label={
          unread === 0 ? "Notifications" : `Notifications, ${unread} unread`
        }
        onClick={() => setOpen((v) => !v)}
      >
        <Bell size={17} />
        {unread > 0 && (
          <span className="bell-badge" aria-hidden="true">
            {badge}
          </span>
        )}
      </button>

      {open && (
        <div className="bell-panel" ref={panelRef} role="dialog" aria-label="Notifications">
          <header>
            <strong>Notifications</strong>
            <div className="bell-actions">
              <button
                type="button"
                className="link-button"
                aria-pressed={unreadOnly}
                onClick={() => setUnreadOnly((v) => !v)}
              >
                {unreadOnly ? "Show all" : "Unread only"}
              </button>
              <button
                type="button"
                className="link-button"
                disabled={busy === "all" || unread === 0}
                onClick={() => void markAllRead()}
              >
                <CheckCheck size={13} /> Mark all read
              </button>
            </div>
          </header>

          <div className="bell-list">
            {loading ? (
              <p className="bell-state">Loading notifications…</p>
            ) : error ? (
              <div className="bell-state">
                <p>{error}</p>
                <button type="button" className="secondary" onClick={() => void load(unreadOnly)}>
                  Try again
                </button>
              </div>
            ) : items.length === 0 ? (
              <p className="bell-state">
                {unreadOnly ? "No unread notifications." : "You're all caught up."}
              </p>
            ) : (
              items.map((n) => (
                <article
                  key={n.id}
                  className={`bell-item sev-${n.severity.toLowerCase()}${n.isRead ? "" : " unread"}`}
                >
                  <div className="bell-item-body">
                    <div className="bell-item-head">
                      <strong>{n.title}</strong>
                      <span className={`sev-chip sev-${n.severity.toLowerCase()}`}>
                        {n.severityLabel}
                      </span>
                    </div>
                    <p>{n.message}</p>
                    <small>{fmtDate(n.createdAtUtc)}</small>
                  </div>
                  <div className="bell-item-actions">
                    {isKnownRoute(n.route) && (
                      <button
                        type="button"
                        className="secondary"
                        onClick={() => void openNotification(n)}
                      >
                        {n.actionLabel}
                      </button>
                    )}
                    {!n.isRead && (
                      <button
                        type="button"
                        className="icon-button"
                        aria-label={`Mark “${n.title}” as read`}
                        onClick={() => void markRead(n)}
                      >
                        <Check size={14} />
                      </button>
                    )}
                    <button
                      type="button"
                      className="icon-button"
                      aria-label={`Dismiss “${n.title}”`}
                      disabled={busy === n.id}
                      onClick={() => void dismiss(n)}
                    >
                      <X size={14} />
                    </button>
                  </div>
                </article>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}
