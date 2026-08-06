'use client';

import 'leaflet/dist/leaflet.css';
import { useEffect, useRef } from 'react';
import type { NetworkProviderMarker } from '@/types/careconnect';
import { formatPhoneDisplay } from '@/lib/phone';

interface MyNetworkMapProps {
  markers:    NetworkProviderMarker[];
  selectedId: string | null;
  onSelect:   (id: string) => void;
}

const US_CENTER: [number, number] = [39.5, -98.35];
const MILES_TO_METERS = 1609.34;

/** Escapes HTML special characters to prevent XSS when injecting provider data into popup innerHTML. */
function esc(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

type L = typeof import('leaflet');

export function MyNetworkMapLeaflet({ markers, selectedId, onSelect }: MyNetworkMapProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef       = useRef<import('leaflet').Map | null>(null);
  const layerRef     = useRef<import('leaflet').LayerGroup | null>(null);

  const onSelectRef = useRef(onSelect);
  onSelectRef.current = onSelect;

  // ── Init map once on mount ────────────────────────────────────────────────
  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    let cancelled = false;
    void (async () => {
      const Leaflet = (await import('leaflet')).default as unknown as L;
      if (cancelled) return;

      // Clear any stale Leaflet state left by React StrictMode's double-mount or HMR.
      (el as HTMLDivElement & { _leaflet_id?: number })._leaflet_id = undefined;

      const withCoords = markers.filter(m => m.latitude && m.longitude);
      const center: [number, number] = withCoords.length > 0
        ? [withCoords[0].latitude, withCoords[0].longitude]
        : US_CENTER;
      const zoom = withCoords.length > 0 ? 10 : 4;

      const map = Leaflet.map(el, { center, zoom, scrollWheelZoom: false });

      Leaflet.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      }).addTo(map);

      layerRef.current = Leaflet.layerGroup().addTo(map);
      mapRef.current   = map;
    })();

    return () => {
      cancelled = true;
      mapRef.current?.remove();
      mapRef.current  = null;
      layerRef.current = null;
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ── Sync markers + selection state ───────────────────────────────────────
  useEffect(() => {
    void (async () => {
      const map   = mapRef.current;
      const layer = layerRef.current;
      if (!map || !layer) return;

      const Leaflet = (await import('leaflet')).default as unknown as L;

      layer.clearLayers();

    const withCoords = markers.filter(m => m.latitude && m.longitude);
    for (const m of withCoords) {
      const selected  = m.id === selectedId;
      const accepting = m.acceptingReferrals;

      const popupEl = document.createElement('div');
      popupEl.style.minWidth = '180px';
      popupEl.innerHTML = `
        <p style="font-weight:600;color:#111827;margin:0 0 2px;font-size:14px">${esc(m.name)}</p>
        ${m.organizationName ? `<p style="color:#6b7280;font-size:12px;margin:0 0 4px">${esc(m.organizationName)}</p>` : ''}
        <p style="color:#6b7280;font-size:12px;margin:0 0 4px">
          ${m.isMobile
            ? `Mobile · ${[m.serviceAreaLabel ?? m.addressLine1, `${m.city}, ${m.state}`].filter(Boolean).map(esc).join(' · ')}${m.serviceRadiusMiles != null ? ` · ${m.serviceRadiusMiles}mi radius` : ''}`
            : `${m.addressLine1 ? `${esc(m.addressLine1)}<br>` : ''}${esc(m.city)}, ${esc(m.state)} ${esc(m.postalCode ?? '')}`}
        </p>
        ${m.phone ? `<p style="color:#6b7280;font-size:12px;margin:0 0 8px">${esc(formatPhoneDisplay(m.phone) ?? m.phone)}</p>` : ''}
        <div style="display:flex;gap:6px;flex-wrap:wrap">
          <span style="display:inline-flex;align-items:center;border-radius:9999px;padding:2px 8px;font-size:10px;font-weight:500;border:1px solid;${m.isActive ? 'background:#ecfdf5;color:#065f46;border-color:#a7f3d0' : 'background:#f9fafb;color:#6b7280;border-color:#e5e7eb'}">
            ${m.isActive ? 'Active' : 'Inactive'}
          </span>
          <span style="display:inline-flex;align-items:center;border-radius:9999px;padding:2px 8px;font-size:10px;font-weight:500;border:1px solid;${accepting ? 'background:#f0fdf4;color:#15803d;border-color:#bbf7d0' : 'background:#fffbeb;color:#b45309;border-color:#fde68a'}">
            ${accepting ? 'Accepting' : 'Not accepting'}
          </span>
        </div>
      `;

      if (m.isMobile && m.serviceRadiusMiles) {
        Leaflet.circle([m.latitude, m.longitude], {
          radius:      m.serviceRadiusMiles * MILES_TO_METERS,
          color:       '#7c3aed',
          weight:      2,
          opacity:     0.8,
          dashArray:   '6, 6',
          fillColor:   '#7c3aed',
          fillOpacity: selected ? 0.12 : 0,
          interactive: false,
        }).addTo(layer);
      }

      const fillColor = selected ? '#2563eb' : accepting ? '#10b981' : '#f59e0b';
      const strokeColor = selected ? '#1d4ed8' : accepting ? '#059669' : '#d97706';
      const marker = m.isMobile
        ? Leaflet.marker([m.latitude, m.longitude], {
            icon: Leaflet.divIcon({
              className: '',
              html: `<div style="width:${selected ? 18 : 14}px;height:${selected ? 18 : 14}px;background:${fillColor};border:${selected ? 3 : 1.5}px solid ${strokeColor};transform:rotate(45deg);box-sizing:border-box"></div>`,
              iconSize: [selected ? 18 : 14, selected ? 18 : 14],
              iconAnchor: [(selected ? 18 : 14) / 2, (selected ? 18 : 14) / 2],
            }),
          })
        : Leaflet.circleMarker([m.latitude, m.longitude], {
            radius:      selected ? 13 : 9,
            fillColor,
            fillOpacity: 0.9,
            color:       strokeColor,
            weight:      selected ? 3 : 1.5,
          });

      marker
        .bindPopup(popupEl)
        .on('click', () => {
          map.setView([m.latitude, m.longitude], Math.max(map.getZoom(), m.isMobile ? 10 : 13));
          onSelectRef.current(m.id);
        })
        .addTo(layer);
    }
    })();
  }, [markers, selectedId]);

  return (
    <div style={{ isolation: 'isolate', height: '480px', width: '100%', borderRadius: '0.75rem' }}>
      <div ref={containerRef} style={{ height: '100%', width: '100%', borderRadius: '0.75rem' }} />
    </div>
  );
}
