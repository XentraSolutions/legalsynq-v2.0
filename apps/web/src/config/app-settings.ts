// ── Types ──────────────────────────────────────────────────────────────────────

export interface NavAppearance {
  /** Colour for active sidebar icon, left accent bar, and active text. */
  activeColor: string;
  /** Background colour for the active sidebar item row. */
  activeBg: string;
}

export interface Appearance {
  nav: NavAppearance;
}

export interface CareConnectSettings {
  requireAvailabilityCheck: boolean;
  /** Set by TenantAdmin in the Tenant Portal → stored on Tenant record in DB. */
  defaultMapProvider: 'osm' | 'google';
}

export interface AppSettings {
  appearance:  Appearance;
  careConnect: CareConnectSettings;
  /** IANA timezone for date/time display. Sourced from Tenant.TimeZone in DB. */
  timezone:    string;
}

// ── Global defaults ────────────────────────────────────────────────────────────

export const GLOBAL_DEFAULTS: AppSettings = {
  appearance: {
    nav: {
      activeColor: '#f97316',   // orange-500
      activeBg:    '#fff7ed',   // orange-50
    },
  },
  careConnect: {
    requireAvailabilityCheck: false,
    defaultMapProvider: 'google',
  },
  timezone: 'America/Los_Angeles',
};
