'use client';

import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from 'react';
import type { TenantBranding } from '@/types';

const DEFAULT_BRANDING: TenantBranding = {
  tenantId:    '',
  tenantCode:  '',
  displayName: 'LegalSynq',
};

const TenantBrandingContext = createContext<TenantBranding>(DEFAULT_BRANDING);

/**
 * Fetches tenant branding from /identity/api/tenants/current/branding.
 * This endpoint is anonymous and keyed to the subdomain via the Host header.
 * Injects CSS variables (--color-primary) and updates the favicon.
 *
 * Loaded before auth — the login page must show correct tenant branding.
 */
export function TenantBrandingProvider({ children }: { children: ReactNode }) {
  const [branding, setBranding] = useState<TenantBranding>(DEFAULT_BRANDING);

  useEffect(() => {
    async function loadBranding() {
      try {
        // In development, no subdomain exists on localhost.
        // Send X-Tenant-Code header so the Identity service can resolve the tenant.
        // In production the Host header (forwarded by the gateway) is sufficient.
        const headers: Record<string, string> = {};
        const devTenantCode = process.env.NEXT_PUBLIC_TENANT_CODE;
        if (devTenantCode) headers['X-Tenant-Code'] = devTenantCode;

        const res = await fetch('/api/identity/api/tenants/current/branding', {
          headers,
          cache: 'no-store',
        });
        if (!res.ok) return;
        const data: TenantBranding = await res.json();
        setBranding(data);
        applyBrandingToDOM(data);
      } catch {
        // Keep default branding on error
      }
    }
    loadBranding();
  }, []);

  return (
    <TenantBrandingContext.Provider value={branding}>
      {children}
    </TenantBrandingContext.Provider>
  );
}

export function useTenantBranding(): TenantBranding {
  return useContext(TenantBrandingContext);
}

// ── DOM mutation helpers ──────────────────────────────────────────────────────

function applyBrandingToDOM(branding: TenantBranding): void {
  if (branding.primaryColor) {
    document.documentElement.style.setProperty('--color-primary', branding.primaryColor);
  }

  if (branding.displayName) {
    document.title = branding.displayName;
  }

  if (branding.faviconUrl) {
    let link = document.querySelector<HTMLLinkElement>("link[rel~='icon']");
    if (!link) {
      link = document.createElement('link');
      link.rel = 'icon';
      document.head.appendChild(link);
    }
    link.href = branding.faviconUrl;
  }
}
