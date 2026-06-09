'use client';

import 'leaflet/dist/leaflet.css';
import { useEffect, useRef } from 'react';
import type { PublicProviderMarker } from '@/lib/public-network-api';

interface NumberedMarker extends PublicProviderMarker {
  index: number;
}

interface PublicNetworkMapProps {
  markers:           NumberedMarker[];
  selectedId:        string | null;
  zoomToId?:         string | null;
  onZoomed?:         () => void;
  onSelect:          (id: string) => void;
  onRequestReferral: (m: PublicProviderMarker) => void;
}

type L = typeof import('leaflet');

const US_CENTER: [number, number] = [39.5, -98.35];

/** Escapes HTML special characters to prevent XSS when injecting provider data into popup innerHTML. */
function esc(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function makePinHtml(index: number, accepting: boolean, selected: boolean): string {
  const bg   = selected ? '#1d4ed8' : accepting ? '#dc2626' : '#6b7280';
  const size = selected ? 34 : 28;
  const font = selected ? 13 : 11;
  const ring = selected ? 'box-shadow:0 0 0 3px #bfdbfe;' : '';
  return `<div style="width:${size}px;height:${size}px;background:${bg};border-radius:50%;display:flex;align-items:center;justify-content:center;color:#fff;font-weight:700;font-size:${font}px;font-family:system-ui,sans-serif;border:2px solid #fff;${ring}box-shadow:0 2px 6px rgba(0,0,0,.35);transition:all .15s;">${index}</div>`;
}

function buildPopupEl(m: NumberedMarker, onReferral: (m: NumberedMarker) => void): HTMLDivElement {
  const el = document.createElement('div');
  el.style.fontFamily = 'system-ui,sans-serif';
  el.style.minWidth   = '220px';
  el.innerHTML = `
    <div style="display:flex;align-items:center;gap:8px;margin-bottom:4px">
      <span style="width:22px;height:22px;border-radius:50%;background:${m.acceptingReferrals ? '#dc2626' : '#6b7280'};color:#fff;display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:700;flex-shrink:0">${m.index}</span>
      <p style="font-weight:700;font-size:14px;color:#111827;margin:0">${esc(m.name)}</p>
    </div>
    ${m.organizationName ? `<p style="font-size:12px;color:#6b7280;margin:0 0 4px">${esc(m.organizationName)}</p>` : ''}
    <p style="font-size:12px;color:#9ca3af;margin:0 0 8px">${esc(m.city)}, ${esc(m.state)}</p>
    ${m.acceptingReferrals
      ? `<span style="font-size:11px;color:#15803d;background:#f0fdf4;border:1px solid #bbf7d0;border-radius:9999px;padding:2px 8px;display:inline-block;margin-bottom:10px">Accepting referrals</span>`
      : `<span style="font-size:11px;color:#6b7280;background:#f9fafb;border:1px solid #e5e7eb;border-radius:9999px;padding:2px 8px;display:inline-block;margin-bottom:10px">Not accepting referrals</span>`
    }
    ${m.acceptingReferrals ? `<button style="font-size:12px;color:#fff;background:#dc2626;border:none;border-radius:6px;padding:6px 14px;cursor:pointer;font-weight:600;display:block;width:100%">Send Referral</button>` : ''}
  `;
  if (m.acceptingReferrals) {
    const btn = el.querySelector<HTMLButtonElement>('button');
    if (btn) btn.addEventListener('click', () => onReferral(m));
  }
  return el;
}

export function PublicNetworkMapLeaflet({ markers, selectedId, zoomToId, onZoomed, onSelect, onRequestReferral }: PublicNetworkMapProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef       = useRef<import('leaflet').Map | null>(null);
  const layerRef     = useRef<import('leaflet').LayerGroup | null>(null);

  // Always-current callback refs — avoids stale closures without adding to effect deps.
  const onSelectRef   = useRef(onSelect);
  const onReferralRef = useRef(onRequestReferral);
  const onZoomedRef   = useRef(onZoomed);
  onSelectRef.current   = onSelect;
  onReferralRef.current = onRequestReferral;
  onZoomedRef.current   = onZoomed;

  // Previous marker identities — used to decide whether to re-fit the map view.
  const prevMarkerIdsRef = useRef('');
  // Always-current marker list for the external-zoom effect (avoids adding markers to its deps).
  const markersRef = useRef(markers);
  markersRef.current = markers;

  // ── Init map once on mount ────────────────────────────────────────────────
  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    let cancelled = false;
    void (async () => {
      const Leaflet = (await import('leaflet')).default as unknown as L;
      if (cancelled) return;

      // Clear any stale Leaflet state left by React StrictMode's double-mount
      // or by HMR module replacement. Leaflet throws "Map container is already
      // initialized" if _leaflet_id is set when new Leaflet.Map() is called.
      (el as HTMLDivElement & { _leaflet_id?: number })._leaflet_id = undefined;

      const map = Leaflet.map(el, { center: US_CENTER, zoom: 4, scrollWheelZoom: true, zoomControl: true });

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
  }, []);

  // ── Sync markers + selection state ───────────────────────────────────────
  useEffect(() => {
    void (async () => {
      const map   = mapRef.current;
      const layer = layerRef.current;
      if (!map || !layer) return;

      const Leaflet = (await import('leaflet')).default as unknown as L;

      layer.clearLayers();

    for (const m of markers) {
      const sel  = m.id === selectedId;
      const size = sel ? 34 : 28;
      const icon = Leaflet.divIcon({
        className:   '',
        iconSize:    [size, size] as [number, number],
        iconAnchor:  [size / 2, size / 2] as [number, number],
        popupAnchor: [0, -(size / 2 + 4)] as [number, number],
        html:        makePinHtml(m.index, m.acceptingReferrals, sel),
      });

      Leaflet
        .marker([m.latitude, m.longitude], { icon, zIndexOffset: sel ? 1000 : 0 })
        .bindPopup(buildPopupEl(m, mk => onReferralRef.current(mk)), { minWidth: 220, closeButton: false })
        .on('click', () => {
          map.setView([m.latitude, m.longitude], Math.max(map.getZoom(), 13));
          onSelectRef.current(m.id);
        })
        .addTo(layer);
    }

    // Re-fit bounds only when the actual marker set changes, not on selectedId changes.
    const newIds = markers.map(m => m.id).join(',');
    if (newIds !== prevMarkerIdsRef.current) {
      prevMarkerIdsRef.current = newIds;
      if (markers.length === 1) {
        map.setView([markers[0].latitude, markers[0].longitude], 12);
      } else if (markers.length > 1) {
        map.fitBounds(
          Leaflet.latLngBounds(markers.map(mk => [mk.latitude, mk.longitude] as [number, number])),
          { padding: [40, 40] },
        );
      }
    }    })();  }, [markers, selectedId]);

  // ── Zoom to an externally commanded provider (e.g. card click in split view) ─
  useEffect(() => {
    if (!zoomToId) return;
    const map = mapRef.current;
    if (!map) return;
    const m = markersRef.current.find(mk => mk.id === zoomToId);
    if (m) {
      map.setView([m.latitude, m.longitude], Math.max(map.getZoom(), 13));
      // Reset zoomToId in the parent so re-clicking the same card triggers a new zoom.
      onZoomedRef.current?.();
    }
  }, [zoomToId]);

  // isolation:isolate creates a stacking context that scopes Leaflet's internal
  // z-indexes (200–800) so they cannot bleed above fixed overlays/modals.
  return (
    <div style={{ height: '100%', width: '100%', isolation: 'isolate' }}>
      <div ref={containerRef} style={{ height: '100%', width: '100%' }} />
    </div>
  );
}
