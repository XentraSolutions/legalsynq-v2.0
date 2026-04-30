'use client';

import { useEffect, useRef, useState } from 'react';
import { GoogleMap, useJsApiLoader, Marker, InfoWindow } from '@react-google-maps/api';
import type { PublicProviderMarker } from '@/lib/public-network-api';
import { googleMapsKey } from '@/lib/use-map-provider';

interface NumberedMarker extends PublicProviderMarker { index: number; }
interface PublicNetworkMapProps {
  markers: NumberedMarker[]; selectedId: string | null;
  onSelect: (id: string) => void; onRequestReferral: (m: PublicProviderMarker) => void;
}

const US_CENTER = { lat: 39.5, lng: -98.35 };

function numberedPinUrl(index: number, accepting: boolean, selected: boolean): string {
  const bg   = selected ? '#1d4ed8' : accepting ? '#dc2626' : '#6b7280';
  const size = selected ? 34 : 28;
  const font = selected ? 13 : 11;
  const ring = selected ? `<circle cx="${size / 2}" cy="${size / 2}" r="${size / 2 + 2}" fill="none" stroke="#bfdbfe" stroke-width="3"/>` : '';
  const svg  = `<svg xmlns="http://www.w3.org/2000/svg" width="${size + 8}" height="${size + 8}">
    ${ring}
    <circle cx="${(size + 8) / 2}" cy="${(size + 8) / 2}" r="${size / 2}" fill="${bg}" stroke="white" stroke-width="2"/>
    <text x="${(size + 8) / 2}" y="${(size + 8) / 2 + font * 0.35}" text-anchor="middle" fill="white" font-family="system-ui,sans-serif" font-size="${font}" font-weight="700">${index}</text>
  </svg>`;
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`;
}

export function PublicNetworkMapGoogle({ markers, selectedId, onSelect, onRequestReferral }: PublicNetworkMapProps) {
  const { isLoaded } = useJsApiLoader({ googleMapsApiKey: googleMapsKey() });
  const mapRef        = useRef<google.maps.Map | null>(null);
  const prevCount     = useRef(0);
  const [activeId, setActiveId] = useState<string | null>(null);

  useEffect(() => {
    const cur = markers.length;
    if (!mapRef.current || cur === 0 || cur === prevCount.current) { prevCount.current = cur; return; }
    prevCount.current = cur;

    if (cur === 1) {
      mapRef.current.setCenter({ lat: markers[0].latitude, lng: markers[0].longitude });
      mapRef.current.setZoom(12);
      return;
    }
    const bounds = new window.google.maps.LatLngBounds();
    markers.forEach(m => bounds.extend({ lat: m.latitude, lng: m.longitude }));
    mapRef.current.fitBounds(bounds, 40);
  }, [markers]);

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
      center={US_CENTER}
      zoom={4}
      onLoad={map => { mapRef.current = map; }}
      options={{ gestureHandling: 'greedy', fullscreenControl: false, mapTypeControl: false }}
    >
      {markers.map(m => {
        const selected = m.id === selectedId;
        const size     = selected ? 42 : 36;

        return (
          <Marker
            key={m.id}
            position={{ lat: m.latitude, lng: m.longitude }}
            icon={{
              url:        numberedPinUrl(m.index, m.acceptingReferrals, selected),
              scaledSize: new window.google.maps.Size(size, size),
              anchor:     new window.google.maps.Point(size / 2, size / 2),
            }}
            zIndex={selected ? 1000 : m.index}
            onClick={() => { onSelect(m.id); setActiveId(m.id); }}
          />
        );
      })}

      {activeMarker && (
        <InfoWindow
          position={{ lat: activeMarker.latitude, lng: activeMarker.longitude }}
          onCloseClick={() => setActiveId(null)}
        >
          <div style={{ fontFamily: 'system-ui,sans-serif', minWidth: 200 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
              <span style={{ width: 22, height: 22, borderRadius: '50%', background: activeMarker.acceptingReferrals ? '#dc2626' : '#6b7280', color: '#fff', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontSize: 11, fontWeight: 700, flexShrink: 0 }}>
                {activeMarker.index}
              </span>
              <p style={{ fontWeight: 700, fontSize: 14, color: '#111827', margin: 0 }}>{activeMarker.name}</p>
            </div>
            {activeMarker.organizationName && (
              <p style={{ fontSize: 12, color: '#6b7280', margin: '0 0 4px' }}>{activeMarker.organizationName}</p>
            )}
            <p style={{ fontSize: 12, color: '#9ca3af', margin: '0 0 8px' }}>{activeMarker.city}, {activeMarker.state}</p>
            {activeMarker.acceptingReferrals ? (
              <span style={{ fontSize: 11, color: '#15803d', background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: 9999, padding: '2px 8px', display: 'inline-block', marginBottom: 10 }}>
                Accepting referrals
              </span>
            ) : (
              <span style={{ fontSize: 11, color: '#6b7280', background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: 9999, padding: '2px 8px', display: 'inline-block', marginBottom: 10 }}>
                Not accepting referrals
              </span>
            )}
            {activeMarker.acceptingReferrals && (
              <button
                onClick={() => { onRequestReferral(activeMarker); setActiveId(null); }}
                style={{ fontSize: 12, color: '#fff', background: '#dc2626', border: 'none', borderRadius: 6, padding: '6px 14px', cursor: 'pointer', fontWeight: 600, display: 'block', width: '100%' }}
              >
                Send Referral
              </button>
            )}
          </div>
        </InfoWindow>
      )}
    </GoogleMap>
  );
}
