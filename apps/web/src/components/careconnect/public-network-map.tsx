'use client';

import 'leaflet/dist/leaflet.css';
import { MapContainer, TileLayer, CircleMarker, Popup } from 'react-leaflet';
import type { PublicProviderMarker } from '@/lib/public-network-api';

const US_CENTER: [number, number] = [39.5, -98.35];

interface PublicNetworkMapProps {
  markers:           PublicProviderMarker[];
  selectedId:        string | null;
  onSelect:          (id: string) => void;
  onRequestReferral: (m: PublicProviderMarker) => void;
}

export function PublicNetworkMap({
  markers,
  selectedId,
  onSelect,
  onRequestReferral,
}: PublicNetworkMapProps) {
  const center = US_CENTER;
  const zoom   = markers.length === 1 ? 11 : 5;

  return (
    <MapContainer
      center={center}
      zoom={zoom}
      style={{ height: '100%', width: '100%' }}
      scrollWheelZoom
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />

      {markers.map(m => {
        const isSelected = m.id === selectedId;
        return (
          <CircleMarker
            key={m.id}
            center={[m.latitude, m.longitude]}
            radius={isSelected ? 11 : 7}
            pathOptions={{
              fillColor:   m.acceptingReferrals ? '#16a34a' : '#6b7280',
              fillOpacity: 0.85,
              color:       isSelected ? '#1d4ed8' : '#ffffff',
              weight:      isSelected ? 3 : 1.5,
            }}
            eventHandlers={{ click: () => onSelect(m.id) }}
          >
            <Popup minWidth={220}>
              <div style={{ fontFamily: 'inherit' }}>
                <p style={{ fontWeight: 600, fontSize: 14, marginBottom: 2, color: '#111827' }}>
                  {m.name}
                </p>
                {m.organizationName && (
                  <p style={{ fontSize: 12, color: '#6b7280', marginBottom: 4 }}>
                    {m.organizationName}
                  </p>
                )}
                <p style={{ fontSize: 12, color: '#9ca3af', marginBottom: 8 }}>
                  {m.city}, {m.state}
                </p>

                {m.acceptingReferrals ? (
                  <span style={{
                    fontSize: 11, color: '#15803d', background: '#f0fdf4',
                    border: '1px solid #bbf7d0', borderRadius: 9999,
                    padding: '2px 8px', display: 'inline-block', marginBottom: 10,
                  }}>
                    Accepting referrals
                  </span>
                ) : (
                  <span style={{
                    fontSize: 11, color: '#6b7280', background: '#f9fafb',
                    border: '1px solid #e5e7eb', borderRadius: 9999,
                    padding: '2px 8px', display: 'inline-block', marginBottom: 10,
                  }}>
                    Not accepting referrals
                  </span>
                )}

                {m.acceptingReferrals && (
                  <div>
                    <button
                      onClick={() => onRequestReferral(m)}
                      style={{
                        fontSize: 12, color: '#ffffff', background: '#2563eb',
                        border: 'none', borderRadius: 6, padding: '5px 12px',
                        cursor: 'pointer', fontWeight: 500, display: 'block', width: '100%',
                      }}
                    >
                      Request Referral
                    </button>
                  </div>
                )}
              </div>
            </Popup>
          </CircleMarker>
        );
      })}
    </MapContainer>
  );
}
