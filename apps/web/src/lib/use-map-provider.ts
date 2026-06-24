'use client';

export type MapProvider = 'osm' | 'google';

export function googleMapsKey(): string {
  return process.env.NEXT_PUBLIC_GOOGLE_MAPS_KEY ?? '';
}
