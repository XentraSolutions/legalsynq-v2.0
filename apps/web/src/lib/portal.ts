/**
 * Portal configuration.
 *
 * Each entry maps a raw subdomain (first hostname segment) to portal-specific
 * behaviour.  Adding a new portal here is the only change needed to restrict
 * shell UI for future product-specific portals.
 *
 * Local dev setup: add aliases to /etc/hosts pointing to 127.0.0.1 so the
 * subdomain-based detection works the same as production:
 *
 *   127.0.0.1   careconnect-demo.localhost
 *   127.0.0.1   provider.localhost
 *
 * Then access via http://careconnect-demo.localhost:3000
 * (Use .localhost — not .local.dev — to avoid Chrome's HSTS enforcement)
 */

export interface PortalConfig {
  productId:       'careconnect' | 'synqlien';
  landingPath:     string;
  showAppSwitcher: boolean;
  showBottomNav:   boolean;
  logoSrc:         string;
  logoLabel:       string;
}

function buildPortalConfigs(): Record<string, PortalConfig> {
  const configs: Record<string, PortalConfig> = {};

  const ccSub = (process.env.PORTAL_CARECONNECT_SUBDOMAIN ?? 'careconnect-demo').trim();
  if (ccSub) configs[ccSub] = {
    productId:       'careconnect',
    landingPath:     '/careconnect/dashboard',
    showAppSwitcher: false,
    showBottomNav:   false,
    logoSrc:         '/careconnect-logo.png',
    logoLabel:       'CareConnect',
  };

  const synqLienSub = (process.env.PORTAL_SYNQLIEN_SUBDOMAIN ?? 'synqlien-demo').trim();
  if (synqLienSub) configs[synqLienSub] = {
    productId:       'synqlien',
    landingPath:     '/funding/dashboard',
    showAppSwitcher: false,
    showBottomNav:   false,
    logoSrc:         '/product-icons/synqlien.png',
    logoLabel:       'SynqLien',
  };

  return configs;
}

export const PORTAL_CONFIGS: Record<string, PortalConfig> = buildPortalConfigs();

// ── Client-side helpers (safe to call in 'use client' components) ─────────────

export function getClientSubdomain(): string | null {
  if (typeof window === 'undefined') return null;
  const host  = window.location.hostname;
  const parts = host.split('.');
  // Standard subdomain (prod): careconnect-demo.legalsynq.com
  if (parts.length >= 3 && parts[0] !== 'www') return parts[0];
  // *.localhost pattern (local dev): careconnect-demo.localhost
  if (parts.length === 2 && parts[1] === 'localhost') return parts[0];
  return null;
}

export function getClientPortalConfig(): PortalConfig | null {
  const sub = getClientSubdomain();
  if (!sub) return null;
  return PORTAL_CONFIGS[sub] ?? null;
}

// ── Server-side helpers (safe to call in server components / route handlers) ──

export function getServerSubdomain(rawHost: string): string | null {
  const host  = (rawHost.split(',')[0] ?? '').trim().split(':')[0].toLowerCase();
  const parts = host.split('.');
  // Standard subdomain (prod): careconnect-demo.legalsynq.com
  if (parts.length >= 3 && parts[0] !== 'www') return parts[0];
  // *.localhost pattern (local dev): careconnect-demo.localhost
  if (parts.length === 2 && parts[1] === 'localhost') return parts[0];
  return null;
}

export function getServerPortalConfig(rawHost: string): PortalConfig | null {
  const sub = getServerSubdomain(rawHost);
  if (!sub) return null;
  return PORTAL_CONFIGS[sub] ?? null;
}
