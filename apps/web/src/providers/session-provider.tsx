'use client';

import {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
  useMemo,
  useRef,
  type ReactNode,
} from 'react';
import type { PlatformSession } from '@/types';

interface SessionContextValue {
  session:       PlatformSession | null;
  isLoading:     boolean;
  refresh:       () => Promise<void>;
  clearSession:  () => void;
}

const SessionContext = createContext<SessionContextValue | null>(null);

const PLATFORM_DEFAULT_TIMEOUT_MINUTES = 30;
const WARNING_LEAD_SECONDS = 60;

/**
 * Fetches session from the BFF /api/auth/me route on mount.
 *
 * The BFF route reads the platform_session HttpOnly cookie, forwards it
 * to the Identity service as Authorization: Bearer, and returns the
 * AuthMeResponse envelope. The browser JS never sees the raw JWT.
 *
 * A 401 response means the session is expired or invalid → redirect to /login.
 *
 * Also implements per-tenant idle session timeout. Activity events (mouse,
 * keyboard, scroll, touch) reset the idle timer. When the tenant-configured
 * idle period elapses, a 60-second warning dialog is shown before auto-logout.
 */
/**
 * Serializable version of PlatformSession for the server→client prop boundary.
 * Date objects cannot cross RSC boundaries, so expiresAt is kept as an ISO string.
 */
export interface SerializableSession extends Omit<PlatformSession, 'expiresAt'> {
  expiresAt: string;
}

/** Re-hydrate a SerializableSession back into a full PlatformSession. */
function deserializeSession(s: SerializableSession): PlatformSession {
  return { ...s, expiresAt: new Date(s.expiresAt) };
}

interface SessionProviderProps {
  children:        ReactNode;
  initialSession?: SerializableSession | null;
}

export function SessionProvider({ children, initialSession }: SessionProviderProps) {
  // Seed state from the SSR-resolved session so the UI is populated instantly.
  // isLoading starts false when we already have data; true only on a cold client load.
  const seeded = initialSession ? deserializeSession(initialSession) : null;
  const [session,   setSession]   = useState<PlatformSession | null>(seeded);
  const [isLoading, setIsLoading] = useState(initialSession == null);
  const [showWarning, setShowWarning] = useState(false);
  const [countdown,   setCountdown]   = useState(WARNING_LEAD_SECONDS);

  const idleTimerRef    = useRef<ReturnType<typeof setTimeout> | null>(null);
  const warningTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const sessionRef      = useRef<PlatformSession | null>(seeded);
  const showWarningRef  = useRef(false);

  const fetchSession = useCallback(async () => {
    // Only show the loading spinner when we have no session at all yet.
    // If we already have an SSR-seeded session this runs as a silent background refresh.
    if (!sessionRef.current) setIsLoading(true);
    try {
      const res = await fetch('/api/auth/me', {
        credentials: 'include',
        cache:       'no-store',
      });

      if (!res.ok) {
        if (res.status === 401) {
          // Genuine auth failure — clear session and redirect to login.
          setSession(null);
          sessionRef.current = null;
          if (typeof window !== 'undefined') {
            window.location.href = '/login';
          }
        }
        // Non-401 errors (503, 500, network blip): keep any existing session
        // so the avatar stays visible. The user is still authenticated —
        // a transient backend error should not log them out silently.
        return;
      }

      const me = await res.json();
      const mapped: PlatformSession = {
        userId:                me.userId,
        email:                 me.email,
        tenantId:              me.tenantId,
        tenantCode:            me.tenantCode,
        orgId:                 me.orgId,
        orgType:               me.orgType,
        orgName:               me.orgName,
        productRoles:          me.productRoles          ?? [],
        systemRoles:           me.systemRoles           ?? [],
        enabledProducts:       me.enabledProducts       ?? [],
        isPlatformAdmin:       (me.systemRoles ?? []).includes('PlatformAdmin'),
        isTenantAdmin:         (me.systemRoles ?? []).includes('TenantAdmin'),
        hasOrg:                !!me.orgId,
        expiresAt:             new Date(me.expiresAtUtc),
        sessionTimeoutMinutes: me.sessionTimeoutMinutes ?? PLATFORM_DEFAULT_TIMEOUT_MINUTES,
      };
      setSession(mapped);
      sessionRef.current = mapped;
    } catch {
      // Network error: preserve any existing session — the avatar should
      // remain visible. Do not clear the session on connectivity failures.
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => { fetchSession(); }, [fetchSession]);

  const clearSession = useCallback(() => {
    setSession(null);
    sessionRef.current = null;
  }, []);

  // ── Idle timeout ────────────────────────────────────────────────────────────

  const doLogout = useCallback(async () => {
    showWarningRef.current = false;
    setShowWarning(false);
    if (warningTimerRef.current) clearInterval(warningTimerRef.current);
    if (idleTimerRef.current)    clearTimeout(idleTimerRef.current);
    await fetch('/api/auth/logout', { method: 'POST' }).catch(() => {});
    clearSession();
    window.location.href = '/login?reason=idle';
  }, [clearSession]);

  const startWarningCountdown = useCallback(() => {
    setCountdown(WARNING_LEAD_SECONDS);
    showWarningRef.current = true;
    setShowWarning(true);
    warningTimerRef.current = setInterval(() => {
      setCountdown(prev => {
        if (prev <= 1) {
          clearInterval(warningTimerRef.current!);
          void doLogout();
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
  }, [doLogout]);

  const resetIdleTimer = useCallback(() => {
    const s = sessionRef.current;
    if (!s) return;

    if (showWarningRef.current) return;

    if (idleTimerRef.current) clearTimeout(idleTimerRef.current);

    const timeoutMs = (s.sessionTimeoutMinutes ?? PLATFORM_DEFAULT_TIMEOUT_MINUTES) * 60 * 1000;
    const warningMs = timeoutMs - WARNING_LEAD_SECONDS * 1000;

    idleTimerRef.current = setTimeout(() => {
      startWarningCountdown();
    }, Math.max(warningMs, 0));
  }, [startWarningCountdown]);

  const stayActive = useCallback(() => {
    if (warningTimerRef.current) clearInterval(warningTimerRef.current);
    showWarningRef.current = false;
    setShowWarning(false);
    setCountdown(WARNING_LEAD_SECONDS);
    resetIdleTimer();
  }, [resetIdleTimer]);

  useEffect(() => {
    if (!session) return;

    const events = ['mousemove', 'mousedown', 'keydown', 'scroll', 'touchstart'] as const;
    const handler = () => resetIdleTimer();

    events.forEach(e => window.addEventListener(e, handler, { passive: true }));
    resetIdleTimer();

    return () => {
      events.forEach(e => window.removeEventListener(e, handler));
      if (idleTimerRef.current)    clearTimeout(idleTimerRef.current);
      if (warningTimerRef.current) clearInterval(warningTimerRef.current);
    };
  }, [session, resetIdleTimer]);

  const ctxValue = useMemo(
    () => ({ session, isLoading, refresh: fetchSession, clearSession }),
    [session, isLoading, fetchSession, clearSession],
  );

  return (
    <SessionContext.Provider value={ctxValue}>
      {children}
      {showWarning && (
        <IdleWarningDialog countdown={countdown} onStay={stayActive} onLogout={doLogout} />
      )}
    </SessionContext.Provider>
  );
}

export function useSessionContext(): SessionContextValue {
  const ctx = useContext(SessionContext);
  if (!ctx) throw new Error('useSessionContext must be used inside <SessionProvider>');
  return ctx;
}

// ── Idle warning dialog ──────────────────────────────────────────────────────

function IdleWarningDialog({
  countdown,
  onStay,
  onLogout,
}: {
  countdown: number;
  onStay: () => void;
  onLogout: () => void;
}) {
  return (
    <div
      className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/60 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="idle-warning-title"
    >
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm mx-4 overflow-hidden">
        <div className="px-6 pt-6 pb-4 text-center">
          <div className="w-14 h-14 rounded-full bg-amber-100 flex items-center justify-center mx-auto mb-4">
            <i className="ri-time-line text-amber-600 text-2xl" />
          </div>
          <h2 id="idle-warning-title" className="text-lg font-semibold text-gray-900 mb-1">
            Session expiring soon
          </h2>
          <p className="text-sm text-gray-500">
            You&apos;ve been inactive. Your session will end in
          </p>
          <p className="text-4xl font-bold text-amber-600 mt-3 tabular-nums">
            {countdown}s
          </p>
        </div>

        <div className="px-6 pb-6 flex gap-3">
          <button
            onClick={onLogout}
            className="flex-1 px-4 py-2.5 rounded-lg border border-gray-200 text-sm font-medium text-gray-600 hover:bg-gray-50 transition-colors"
          >
            Log out
          </button>
          <button
            onClick={onStay}
            className="flex-1 px-4 py-2.5 rounded-lg bg-indigo-600 text-white text-sm font-semibold hover:bg-indigo-700 transition-colors"
          >
            Stay logged in
          </button>
        </div>
      </div>
    </div>
  );
}
