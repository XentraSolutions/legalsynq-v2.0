'use client';

import 'leaflet/dist/leaflet.css';
import { useEffect, useRef } from 'react';
import type { ProviderMarker } from '@/types/careconnect';

interface ViewportBounds {
  northLat: number;
  southLat: number;
  eastLng:  number;
  westLng:  number;
}

interface ProviderMapProps {
  markers:           ProviderMarker[];
  selectedId:        string | null;
  onSelect:          (id: string) => void;
  onViewportChange:  (bounds: ViewportBounds) => void;
  isReferrer:        boolean;
  centerLat?:        number;
  centerLng?:        number;
  defaultZoom?:      number;
}

type L = typeof import('leaflet');

const US_CENTER: [number, number] = [39.5, -98.35];

/** Escapes HTML special characters to prevent XSS when injecting provider data into popup innerHTML. */
function esc(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

export function ProviderMapLeaflet({
  markers,
  selectedId,
  onSelect,
  onViewportChange,
  isReferrer,
  centerLat,
  centerLng,
  defaultZoom = 5,
}: ProviderMapProps) {
  const containerRef      = useRef<HTMLDivElement>(null);
  const mapRef            = useRef<import('leaflet').Map | null>(null);
  const layerRef          = useRef<import('leaflet').LayerGroup | null>(null);
  const viewportTimerRef  = useRef<ReturnType<typeof setTimeout>>();

  const onSelectRef          = useRef(onSelect);
  const onViewportChangeRef  = useRef(onViewportChange);
  const isReferrerRef        = useRef(isReferrer);
  onSelectRef.current         = onSelect;
  onViewportChangeRef.current = onViewportChange;
  isReferrerRef.current       = isReferrer;

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

      const center: [number, number] =
        centerLat != null && centerLng != null ? [centerLat, centerLng] : US_CENTER;
      const zoom = centerLat != null ? 11 : defaultZoom;

      const map = Leaflet.map(el, { center, zoom, scrollWheelZoom: true });

      Leaflet.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      }).addTo(map);

      // Emit viewport bounds after move/zoom with 350 ms debounce.
      const fireBounds = () => {
        clearTimeout(viewportTimerRef.current);
        viewportTimerRef.current = setTimeout(() => {
          const b = map.getBounds();
          onViewportChangeRef.current({
            northLat: b.getNorth(),
            southLat: b.getSouth(),
            eastLng:  b.getEast(),
            westLng:  b.getWest(),
          });
        }, 350);
      };
      map.on('moveend', fireBounds);
      map.on('zoomend', fireBounds);

      layerRef.current = Leaflet.layerGroup().addTo(map);
      mapRef.current   = map;
    })();

    return () => {
      cancelled = true;
      clearTimeout(viewportTimerRef.current);
      mapRef.current?.remove();
      mapRef.current  = null;
      layerRef.current = null;
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ── Re-center when search location prop changes ───────────────────────────
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    if (centerLat != null && centerLng != null) {
      map.setView([centerLat, centerLng], 11);
    }
  }, [centerLat, centerLng]);

  // ── Sync markers + selection state ───────────────────────────────────────
  useEffect(() => {
    void (async () => {
      const map   = mapRef.current;
      const layer = layerRef.current;
      if (!map || !layer) return;

      const Leaflet = (await import('leaflet')).default as unknown as L;

      layer.clearLayers();

    for (const m of markers) {
      const isSelected = m.id === selectedId;

      const popupEl = document.createElement('div');
      popupEl.style.fontFamily = 'inherit';
      popupEl.style.minWidth   = '200px';
      popupEl.innerHTML = `
        <p style="font-weight:600;font-size:14px;margin:0 0 2px;color:#111827">${esc(m.displayLabel)}</p>
        <p style="font-size:12px;color:#6b7280;margin:0 0 6px">${esc(m.markerSubtitle)}</p>
        ${m.acceptingReferrals
          ? `<span style="font-size:11px;color:#15803d;background:#f0fdf4;border:1px solid #bbf7d0;border-radius:9999px;padding:2px 8px;display:inline-block;margin-bottom:8px">Accepting referrals</span>`
          : `<span style="font-size:11px;color:#6b7280;background:#f9fafb;border:1px solid #e5e7eb;border-radius:9999px;padding:2px 8px;display:inline-block;margin-bottom:8px">Not accepting referrals</span>`
        }
        <div style="display:flex;flex-direction:column;gap:4px;margin-top:4px">
          <a href="/careconnect/providers/${encodeURIComponent(m.id)}" style="font-size:12px;color:#2563eb;font-weight:500;text-decoration:none;display:block">View Provider →</a>
          ${isReferrerRef.current && m.acceptingReferrals
            ? `<a href="/careconnect/providers/${encodeURIComponent(m.id)}" style="font-size:12px;color:#7c3aed;text-decoration:none;display:block">Create Referral →</a>`
            : ''
          }
        </div>
      `;

      Leaflet.circleMarker([m.latitude, m.longitude], {
        radius:      isSelected ? 11 : 7,
        fillColor:   m.acceptingReferrals ? '#16a34a' : '#6b7280',
        fillOpacity: 0.85,
        color:       isSelected ? '#1d4ed8' : '#ffffff',
        weight:      isSelected ? 3 : 1.5,
      })
        .bindPopup(popupEl, { minWidth: 200 })
        .on('click', () => {
          map.setView([m.latitude, m.longitude], Math.max(map.getZoom(), 13));
          onSelectRef.current(m.id);
        })
        .addTo(layer);
    }
    })();
  }, [markers, selectedId]);

  return (
    <div style={{ height: '100%', width: '100%', isolation: 'isolate' }}>
      <div ref={containerRef} style={{ height: '100%', width: '100%' }} />
    </div>
  );
}
