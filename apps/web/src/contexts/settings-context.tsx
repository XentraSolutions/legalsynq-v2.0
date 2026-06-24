'use client';

import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { GLOBAL_DEFAULTS, type AppSettings } from '@/config/app-settings';

const SettingsContext = createContext<AppSettings>(GLOBAL_DEFAULTS);

/**
 * Provides app settings to the React tree.
 * `initialMapProvider` and `initialTimezone` are fetched server-side from the
 * Tenant Service and passed down from the platform layout — no client-side fetch.
 */
export function SettingsProvider({
  children,
  initialMapProvider,
  initialTimezone,
}: {
  children:            ReactNode;
  initialMapProvider?: 'osm' | 'google';
  initialTimezone?:    string;
}) {
  const settings = useMemo<AppSettings>(() => ({
    ...GLOBAL_DEFAULTS,
    careConnect: {
      ...GLOBAL_DEFAULTS.careConnect,
      defaultMapProvider: initialMapProvider ?? GLOBAL_DEFAULTS.careConnect.defaultMapProvider,
    },
    timezone: initialTimezone ?? GLOBAL_DEFAULTS.timezone,
  }), [initialMapProvider, initialTimezone]);

  return (
    <SettingsContext.Provider value={settings}>
      {children}
    </SettingsContext.Provider>
  );
}

export function useSettings(): AppSettings {
  return useContext(SettingsContext);
}
