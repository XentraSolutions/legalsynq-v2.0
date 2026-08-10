'use client';

import dynamic from 'next/dynamic';
import { googleMapsKey } from '@/lib/use-map-provider';
import { useSettings } from '@/contexts/settings-context';
import type { PublicProviderMarker } from '@/lib/public-network-api';

export interface NumberedMarker extends PublicProviderMarker {
  index: number;
}

export interface SearchLocationMarker {
  latitude:  number;
  longitude: number;
  label:     string;
}

export interface PublicNetworkMapProps {
  markers:           NumberedMarker[];
  selectedId:        string | null;
  zoomToId?:         string | null;
  onZoomed?:         () => void;
  searchLocation?:   SearchLocationMarker | null;
  onSelect:          (id: string) => void;
  onRequestReferral: (m: PublicProviderMarker) => void;
}

const LeafletMap = dynamic(
  () => import('./public-network-map-leaflet').then(m => m.PublicNetworkMapLeaflet),
  { ssr: false },
);

const GoogleMap = dynamic(
  () => import('./public-network-map-google').then(m => m.PublicNetworkMapGoogle),
  { ssr: false },
);

export function PublicNetworkMap(props: PublicNetworkMapProps) {
  const { careConnect } = useSettings();
  const hasGoogleKey = !!googleMapsKey();

  if (careConnect.defaultMapProvider === 'google' && hasGoogleKey) {
    return <GoogleMap {...props} />;
  }
  return <LeafletMap {...props} />;
}
