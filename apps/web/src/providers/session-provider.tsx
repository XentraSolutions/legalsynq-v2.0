'use client';

import {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
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

/**
 * Fetches session from /identity/api/auth/me on mount.
 *
 * The frontend does NOT decode the raw JWT from the HttpOnly cookie directly.
 * /auth/me is the server-validated source of truth for all session data.
 * A 401 response means the session is expired or invalid — redirect to /login.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const [session,   setSession]   = useState<PlatformSession | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const fetchSession = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await fetch('/api/identity/api/auth/me', {
        credentials: 'include',
        cache:       'no-store',
      });

      if (!res.ok) {
        setSession(null);
        if (res.status === 401 && typeof window !== 'undefined') {
          window.location.href = '/login';
        }
        return;
      }

      const me = await res.json();
      const mapped: PlatformSession = {
        userId:          me.userId,
        email:           me.email,
        tenantId:        me.tenantId,
        tenantCode:      me.tenantCode,
        orgId:           me.orgId,
        orgType:         me.orgType,
        orgName:         me.orgName,
        productRoles:    me.productRoles   ?? [],
        systemRoles:     me.systemRoles    ?? [],
        isPlatformAdmin: (me.systemRoles ?? []).includes('PlatformAdmin'),
        isTenantAdmin:   (me.systemRoles ?? []).includes('TenantAdmin'),
        hasOrg:          !!me.orgId,
        expiresAt:       new Date(me.expiresAtUtc),
      };
      setSession(mapped);
    } catch {
      setSession(null);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => { fetchSession(); }, [fetchSession]);

  const clearSession = useCallback(() => setSession(null), []);

  return (
    <SessionContext.Provider value={{ session, isLoading, refresh: fetchSession, clearSession }}>
      {children}
    </SessionContext.Provider>
  );
}

export function useSessionContext(): SessionContextValue {
  const ctx = useContext(SessionContext);
  if (!ctx) throw new Error('useSessionContext must be used inside <SessionProvider>');
  return ctx;
}
