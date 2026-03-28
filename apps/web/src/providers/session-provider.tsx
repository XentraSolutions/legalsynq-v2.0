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
 * Fetches session from the BFF /api/auth/me route on mount.
 *
 * The BFF route reads the platform_session HttpOnly cookie, forwards it
 * to the Identity service as Authorization: Bearer, and returns the
 * AuthMeResponse envelope. The browser JS never sees the raw JWT.
 *
 * A 401 response means the session is expired or invalid → redirect to /login.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const [session,   setSession]   = useState<PlatformSession | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const fetchSession = useCallback(async () => {
    setIsLoading(true);
    try {
      // /api/auth/me is the Next.js BFF route (not a direct gateway call)
      // The browser sends the platform_session HttpOnly cookie automatically.
      const res = await fetch('/api/auth/me', {
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
