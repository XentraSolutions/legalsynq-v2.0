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
  /** Set by TenantAdmin in the Tenant Portal → stored in TenantSetting DB. */
  defaultMapProvider: 'osm' | 'google';
}

export interface AppSettings {
  appearance: Appearance;
  careConnect: CareConnectSettings;
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
};
