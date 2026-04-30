'use client';

import { useCallback, useRef, useState } from 'react';
import { GoogleMap, useJsApiLoader, Marker, InfoWindow } from '@react-google-maps/api';
import { googleMapsKey } from '@/lib/use-map-provider';
import type { ProviderMarker } from '@/types/careconnect';

interface ViewportBounds { northLat: number; southLat: number; eastLng: number; westLng: number; }
interface ProviderMapProps {
  markers: ProviderMarker[]; selectedId: string | null; onSelect: (id: string) => void;
  onViewportChange: (bounds: ViewportBounds) => void; isReferrer: boolean;
  centerLat?: number; centerLng?: number; defaultZoom?: number;
}

const US_CENTER = { lat: 39.5, lng: -98.35 };

function circleIconUrl(fill: string, stroke: string, radius: number, sw: number): string {
  const size = (radius + sw) * 2;
  const c    = size / 2;
  const svg  = `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}"><circle cx="${c}" cy="${c}" r="${radius}" fill="${fill}" fill-opacity="0.85" stroke="${stroke}" stroke-width="${sw}"/></svg>`;
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`;
}

export function ProviderMapGoogle({
  markers,
  selectedId,
  onSelect,
  onViewportChange,
  isReferrer,
  centerLat,
  centerLng,
  defaultZoom = 5,
}: ProviderMapProps) {
  const { isLoaded } = useJsApiLoader({ googleMapsApiKey: googleMapsKey() });
  const mapRef        = useRef<google.maps.Map | null>(null);
  const timerRef      = useRef<ReturnType<typeof setTimeout>>();
  const [activeId, setActiveId] = useState<string | null>(null);

  const mapCenter = centerLat != null && centerLng != null
    ? { lat: centerLat, lng: centerLng }
    : US_CENTER;
  const zoom = centerLat != null ? 11 : defaultZoom;

  const onLoad = useCallback((map: google.maps.Map) => { mapRef.current = map; }, []);

  const fireBounds = useCallback(() => {
    clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => {
      const b = mapRef.current?.getBounds();
      if (!b) return;
      onViewportChange({
        northLat: b.getNorthEast().lat(),
        southLat: b.getSouthWest().lat(),
        eastLng:  b.getNorthEast().lng(),
        westLng:  b.getSouthWest().lng(),
      });
    }, 350);
  }, [onViewportChange]);

  if (!isLoaded) {
    return (
      <div style={{ height: '100%', width: '100%', background: '#e5e7eb', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#6b7280', fontSize: 14 }}>
        Loading map…
      </div>
    );
  }

  const activeMarker = activeId ? markers.find(x => x.id === activeId) : null;

  return (
    <GoogleMap
      mapContainerStyle={{ height: '100%', width: '100%' }}
      center={mapCenter}
      zoom={zoom}
      onLoad={onLoad}
      onBoundsChanged={fireBounds}
      options={{ gestureHandling: 'greedy', fullscreenControl: false, mapTypeControl: false }}
    >
      {markers.map(m => {
        const isSelected = m.id === selectedId;
        const fill   = m.acceptingReferrals ? '#16a34a' : '#6b7280';
        const stroke = isSelected ? '#1d4ed8' : '#ffffff';
        const radius = isSelected ? 11 : 7;
        const sw     = isSelected ? 3 : 1.5;
        const size   = (radius + sw) * 2;

        return (
          <Marker
            key={m.id}
            position={{ lat: m.latitude, lng: m.longitude }}
            icon={{
              url:        circleIconUrl(fill, stroke, radius, sw),
              scaledSize: new window.google.maps.Size(size, size),
              anchor:     new window.google.maps.Point(size / 2, size / 2),
            }}
            zIndex={isSelected ? 100 : 1}
            onClick={() => { onSelect(m.id); setActiveId(m.id); }}
          />
        );
      })}

      {activeMarker && (
        <InfoWindow
          position={{ lat: activeMarker.latitude, lng: activeMarker.longitude }}
          onCloseClick={() => setActiveId(null)}
        >
          <div style={{ fontFamily: 'inherit', minWidth: 180 }}>
            <p style={{ fontWeight: 600, fontSize: 14, marginBottom: 2, color: '#111827' }}>
              {activeMarker.displayLabel}
            </p>
            <p style={{ fontSize: 12, color: '#6b7280', marginBottom: 6 }}>
              {activeMarker.markerSubtitle}
            </p>
            {activeMarker.acceptingReferrals ? (
              <span style={{ fontSize: 11, color: '#15803d', background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: 9999, padding: '2px 8px', display: 'inline-block', marginBottom: 8 }}>
                Accepting referrals
              </span>
            ) : (
              <span style={{ fontSize: 11, color: '#6b7280', background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: 9999, padding: '2px 8px', display: 'inline-block', marginBottom: 8 }}>
                Not accepting referrals
              </span>
            )}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 4 }}>
              <a href={`/careconnect/providers/${activeMarker.id}`} style={{ fontSize: 12, color: '#2563eb', fontWeight: 500, textDecoration: 'none' }}>
                View Provider →
              </a>
              {isReferrer && activeMarker.acceptingReferrals && (
                <a href={`/careconnect/providers/${activeMarker.id}`} style={{ fontSize: 12, color: '#7c3aed', textDecoration: 'none' }}>
                  Create Referral →
                </a>
              )}
            </div>
          </div>
        </InfoWindow>
      )}
    </GoogleMap>
  );
}
