'use client';

import { useState } from 'react';

export type MapProvider = 'osm' | 'google';

const STORAGE_KEY = 'map_provider';

function readProvider(): MapProvider {
  if (typeof window === 'undefined') return 'osm';
  try {
    const v = localStorage.getItem(STORAGE_KEY);
    return v === 'google' ? 'google' : 'osm';
  } catch {
    return 'osm';
  }
}

export function useMapProvider(): [MapProvider, (p: MapProvider) => void] {
  const [provider, setProvider] = useState<MapProvider>(readProvider);

  const update = (p: MapProvider) => {
    try { localStorage.setItem(STORAGE_KEY, p); } catch { /* ignore */ }
    setProvider(p);
  };

  return [provider, update];
}

export function googleMapsKey(): string {
  return process.env.NEXT_PUBLIC_GOOGLE_MAPS_KEY ?? '';
}
