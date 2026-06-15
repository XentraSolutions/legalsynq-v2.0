'use client';

import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { GLOBAL_DEFAULTS, type AppSettings } from '@/config/app-settings';

const SettingsContext = createContext<AppSettings>(GLOBAL_DEFAULTS);

/**
 * Provides app settings to the React tree.
 * `initialMapProvider` is fetched server-side from the Tenant Service and
 * passed down from the platform layout — no client-side fetch needed.
 */
export function SettingsProvider({
  children,
  initialMapProvider,
}: {
  children:            ReactNode;
  initialMapProvider?: 'osm' | 'google';
}) {
  const settings = useMemo<AppSettings>(() => ({
    ...GLOBAL_DEFAULTS,
    careConnect: {
      ...GLOBAL_DEFAULTS.careConnect,
      defaultMapProvider: initialMapProvider ?? GLOBAL_DEFAULTS.careConnect.defaultMapProvider,
    },
  }), [initialMapProvider]);

  return (
    <SettingsContext.Provider value={settings}>
      {children}
    </SettingsContext.Provider>
  );
}

export function useSettings(): AppSettings {
  return useContext(SettingsContext);
}
