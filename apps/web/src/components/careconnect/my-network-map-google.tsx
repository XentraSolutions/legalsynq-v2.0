'use client';

import { useState } from 'react';
import { GoogleMap, useJsApiLoader, Marker, InfoWindow } from '@react-google-maps/api';
import type { NetworkProviderMarker } from '@/types/careconnect';
import { formatPhoneDisplay } from '@/lib/phone';
import { googleMapsKey } from '@/lib/use-map-provider';

interface MyNetworkMapProps {
  markers: NetworkProviderMarker[]; selectedId: string | null; onSelect: (id: string) => void;
}

const US_CENTER = { lat: 39.5, lng: -98.35 };

function circleIconUrl(fill: string, stroke: string, radius: number, sw: number): string {
  const size = (radius + sw) * 2;
  const c    = size / 2;
  const svg  = `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}"><circle cx="${c}" cy="${c}" r="${radius}" fill="${fill}" fill-opacity="0.9" stroke="${stroke}" stroke-width="${sw}"/></svg>`;
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`;
}

export function MyNetworkMapGoogle({ markers, selectedId, onSelect }: MyNetworkMapProps) {
  const { isLoaded } = useJsApiLoader({ googleMapsApiKey: googleMapsKey() });
  const [activeId, setActiveId] = useState<string | null>(null);

  const withCoords = markers.filter(m => m.latitude && m.longitude);
  const mapCenter = withCoords.length > 0
    ? { lat: withCoords[0].latitude, lng: withCoords[0].longitude }
    : US_CENTER;
  const zoom = withCoords.length > 0 ? 10 : 4;

  if (!isLoaded) {
    return (
      <div style={{ height: '480px', width: '100%', borderRadius: '0.75rem', background: '#e5e7eb', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#6b7280', fontSize: 14 }}>
        Loading map…
      </div>
    );
  }

  const activeMarker = activeId ? withCoords.find(x => x.id === activeId) : null;

  return (
    <GoogleMap
      mapContainerStyle={{ height: '480px', width: '100%', borderRadius: '0.75rem' }}
      center={mapCenter}
      zoom={zoom}
      options={{ gestureHandling: 'cooperative', scrollwheel: false, fullscreenControl: false, mapTypeControl: false }}
    >
      {withCoords.map(m => {
        const selected  = m.id === selectedId;
        const accepting = m.acceptingReferrals;
        const fill   = selected ? '#2563eb' : accepting ? '#10b981' : '#f59e0b';
        const stroke = selected ? '#1d4ed8' : accepting ? '#059669' : '#d97706';
        const radius = selected ? 13 : 9;
        const sw     = selected ? 3 : 1.5;
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
            zIndex={selected ? 100 : 1}
            onClick={() => { onSelect(m.id); setActiveId(m.id); }}
          />
        );
      })}

      {activeMarker && (
        <InfoWindow
          position={{ lat: activeMarker.latitude, lng: activeMarker.longitude }}
          onCloseClick={() => setActiveId(null)}
        >
          <div style={{ fontFamily: 'system-ui,sans-serif', minWidth: 180 }}>
            <p style={{ fontWeight: 600, fontSize: 14, color: '#111827', margin: '0 0 2px' }}>{activeMarker.name}</p>
            {activeMarker.organizationName && (
              <p style={{ fontSize: 12, color: '#6b7280', margin: '0 0 4px' }}>{activeMarker.organizationName}</p>
            )}
            <p style={{ fontSize: 12, color: '#6b7280', margin: '0 0 4px' }}>
              {activeMarker.addressLine1 && <>{activeMarker.addressLine1}<br /></>}
              {activeMarker.city}, {activeMarker.state} {activeMarker.postalCode}
            </p>
            {activeMarker.phone && (
              <p style={{ fontSize: 12, color: '#6b7280', margin: '0 0 8px' }}>{formatPhoneDisplay(activeMarker.phone)}</p>
            )}
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
              <span style={{ fontSize: 10, fontWeight: 600, padding: '2px 8px', borderRadius: 9999, border: '1px solid', background: activeMarker.isActive ? '#ecfdf5' : '#f9fafb', color: activeMarker.isActive ? '#065f46' : '#6b7280', borderColor: activeMarker.isActive ? '#a7f3d0' : '#e5e7eb' }}>
                {activeMarker.isActive ? 'Active' : 'Inactive'}
              </span>
              <span style={{ fontSize: 10, fontWeight: 600, padding: '2px 8px', borderRadius: 9999, border: '1px solid', background: activeMarker.acceptingReferrals ? '#f0fdf4' : '#fffbeb', color: activeMarker.acceptingReferrals ? '#15803d' : '#92400e', borderColor: activeMarker.acceptingReferrals ? '#bbf7d0' : '#fcd34d' }}>
                {activeMarker.acceptingReferrals ? 'Accepting' : 'Not accepting'}
              </span>
            </div>
          </div>
        </InfoWindow>
      )}
    </GoogleMap>
  );
}
