'use client';

import { useEffect, useState, type ReactNode } from 'react';
import { SettingsProvider } from '@/contexts/settings-context';
import { apiClient } from '@/lib/api-client';

type MapProvider = 'google' | 'osm';

interface PublicNetworkShellProps {
  tenantId: string;
  children: ReactNode;
}

export function PublicNetworkShell({ tenantId, children }: PublicNetworkShellProps) {
  const [mapProvider, setMapProvider] = useState<MapProvider>('google');

  useEffect(() => {
    let cancelled = false;

    async function loadMapProvider() {
      try {
        const { data } = await apiClient.get<{ value?: string }>(
          `/public/careconnect/map-provider?tenantId=${encodeURIComponent(tenantId)}`,
        );

        if (!cancelled) {
          setMapProvider(data.value === 'osm' ? 'osm' : 'google');
        }
      } catch {
        if (!cancelled) {
          setMapProvider('google');
        }
      }
    }

    void loadMapProvider();

    return () => {
      cancelled = true;
    };
  }, [tenantId]);

  return (
    <SettingsProvider initialMapProvider={mapProvider}>
      {children}
    </SettingsProvider>
  );
}
