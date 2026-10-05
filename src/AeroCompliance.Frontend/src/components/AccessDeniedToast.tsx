import { useEffect, useState } from "react";

const EVENT = "aerocompliance:access-denied";
const VISIBLE_MS = 4000;

/** Fired by the route guard right before it redirects away from a forbidden URL. */
export function announceAccessDenied() {
  window.dispatchEvent(new Event(EVENT));
}

/**
 * A single, short-lived toast for the "you tried to open something you can't" case. Mounted once
 * at the app root — outside the routed area — so it survives the redirect that immediately
 * follows the event and is never duplicated by nested layouts.
 */
export default function AccessDeniedToast() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    let timer: number | undefined;
    const onDenied = () => {
      setVisible(true);
      window.clearTimeout(timer);
      timer = window.setTimeout(() => setVisible(false), VISIBLE_MS);
    };
    window.addEventListener(EVENT, onDenied);
    return () => {
      window.removeEventListener(EVENT, onDenied);
      window.clearTimeout(timer);
    };
  }, []);

  if (!visible) return null;
  return (
    <div className="access-toast" role="status" aria-live="polite">
      You do not have access to that area.
    </div>
  );
}
