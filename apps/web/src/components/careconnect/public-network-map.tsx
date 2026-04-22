'use client';

import 'leaflet/dist/leaflet.css';
import { useEffect, useRef } from 'react';
import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet';
import type { PublicProviderMarker } from '@/lib/public-network-api';

export interface NumberedMarker extends PublicProviderMarker {
  index: number;
}

interface PublicNetworkMapProps {
  markers:           NumberedMarker[];
  selectedId:        string | null;
  onSelect:          (id: string) => void;
  onRequestReferral: (m: PublicProviderMarker) => void;
}

const US_CENTER: [number, number] = [39.5, -98.35];

/**
 * Build a numbered circle pin using Leaflet's divIcon.
 * Leaflet is NOT imported at the module level (it accesses `window` at init time
 * and would break SSR even behind a dynamic import).  Instead we require() it
 * lazily inside this function, which only runs on the client.
 */
function makePinIcon(index: number, accepting: boolean, selected: boolean) {
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const L = require('leaflet') as typeof import('leaflet');

  const bg   = selected ? '#1d4ed8' : accepting ? '#dc2626' : '#6b7280';
  const size = selected ? 34 : 28;
  const font = selected ? 13 : 11;
  const ring = selected ? 'box-shadow:0 0 0 3px #bfdbfe;' : '';

  return L.divIcon({
    className:   '',
    iconSize:    [size, size],
    iconAnchor:  [size / 2, size / 2],
    popupAnchor: [0, -(size / 2 + 4)],
    html: `
      <div style="
        width:${size}px;height:${size}px;background:${bg};border-radius:50%;
        display:flex;align-items:center;justify-content:center;
        color:#fff;font-weight:700;font-size:${font}px;font-family:system-ui,sans-serif;
        border:2px solid #fff;${ring}
        box-shadow:0 2px 6px rgba(0,0,0,.35);transition:all .15s;
      ">${index}</div>
    `,
  });
}

/**
 * Fits the map to the given markers whenever they first become available.
 * Runs on mount and again whenever the marker count grows from 0 → N,
 * which handles the async geocoding case.
 */
function FlyToMarkers({ markers }: { markers: NumberedMarker[] }) {
  const map  = useMap();
  const prev = useRef(0);

  useEffect(() => {
    const cur = markers.length;
    // Only act when we transition from no-markers to markers
    if (cur === 0 || prev.current === cur) { prev.current = cur; return; }
    prev.current = cur;

    if (cur === 1) {
      map.setView([markers[0].latitude, markers[0].longitude], 12);
      return;
    }
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const L = require('leaflet') as typeof import('leaflet');
    const bounds = L.latLngBounds(
      markers.map(m => [m.latitude, m.longitude] as [number, number]),
    );
    map.fitBounds(bounds, { padding: [40, 40] });
  }, [map, markers]);

  return null;
}

export function PublicNetworkMap({
  markers,
  selectedId,
  onSelect,
  onRequestReferral,
}: PublicNetworkMapProps) {
  return (
    <MapContainer
      center={US_CENTER}
      zoom={4}
      style={{ height: '100%', width: '100%' }}
      scrollWheelZoom
      zoomControl
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />

      <FlyToMarkers markers={markers} />

      {markers.map(m => (
        <Marker
          key={m.id}
          position={[m.latitude, m.longitude]}
          icon={makePinIcon(m.index, m.acceptingReferrals, m.id === selectedId)}
          eventHandlers={{ click: () => onSelect(m.id) }}
          zIndexOffset={m.id === selectedId ? 1000 : 0}
        >
          <Popup minWidth={220} closeButton={false}>
            <div style={{ fontFamily: 'system-ui,sans-serif' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                <span style={{
                  width: 22, height: 22, borderRadius: '50%',
                  background: m.acceptingReferrals ? '#dc2626' : '#6b7280',
                  color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 11, fontWeight: 700, flexShrink: 0,
                }}>{m.index}</span>
                <p style={{ fontWeight: 700, fontSize: 14, color: '#111827', margin: 0 }}>
                  {m.name}
                </p>
              </div>
              {m.organizationName && (
                <p style={{ fontSize: 12, color: '#6b7280', margin: '0 0 4px' }}>
                  {m.organizationName}
                </p>
              )}
              <p style={{ fontSize: 12, color: '#9ca3af', margin: '0 0 8px' }}>
                {m.city}, {m.state}
              </p>
              {m.acceptingReferrals ? (
                <span style={{
                  fontSize: 11, color: '#15803d', background: '#f0fdf4',
                  border: '1px solid #bbf7d0', borderRadius: 9999,
                  padding: '2px 8px', display: 'inline-block', marginBottom: 10,
                }}>Accepting referrals</span>
              ) : (
                <span style={{
                  fontSize: 11, color: '#6b7280', background: '#f9fafb',
                  border: '1px solid #e5e7eb', borderRadius: 9999,
                  padding: '2px 8px', display: 'inline-block', marginBottom: 10,
                }}>Not accepting referrals</span>
              )}
              {m.acceptingReferrals && (
                <div>
                  <button
                    onClick={() => onRequestReferral(m)}
                    style={{
                      fontSize: 12, color: '#fff', background: '#dc2626',
                      border: 'none', borderRadius: 6, padding: '6px 14px',
                      cursor: 'pointer', fontWeight: 600, display: 'block', width: '100%',
                    }}
                  >
                    Request Referral
                  </button>
                </div>
              )}
            </div>
          </Popup>
        </Marker>
      ))}
    </MapContainer>
  );
}
