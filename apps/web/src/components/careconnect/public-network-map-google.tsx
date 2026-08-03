'use client';

import { useRef, useEffect } from 'react';
import { useGoogleMapsScript } from '@/lib/use-google-maps-script';
import type { PublicProviderMarker } from '@/lib/public-network-api';

interface NumberedMarker extends PublicProviderMarker { index: number; }
interface SearchLocationMarker { latitude: number; longitude: number; label: string; }
interface PublicNetworkMapProps {
  markers: NumberedMarker[];
  selectedId: string | null;
  zoomToId?: string | null;
  onZoomed?: () => void;
  searchLocation?: SearchLocationMarker | null;
  onSelect: (id: string) => void;
  onRequestReferral: (m: PublicProviderMarker) => void;
}

const US_CENTER = { lat: 39.5, lng: -98.35 };

function numberedPinUrl(index: number, accepting: boolean, selected: boolean): string {
  const bg   = selected ? '#1d4ed8' : accepting ? '#dc2626' : '#6b7280';
  const size = selected ? 34 : 28;
  const font = selected ? 13 : 11;
  const c    = size / 2;
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}">` +
    `<circle cx="${c}" cy="${c}" r="${c - 2}" fill="${bg}" stroke="white" stroke-width="2"/>` +
    `<text x="${c}" y="${c + font * 0.38}" text-anchor="middle" fill="white" font-family="system-ui,sans-serif" font-size="${font}" font-weight="700">${index}</text>` +
    `</svg>`,
  )}`;
}

function searchPinUrl(): string {
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" width="34" height="34" viewBox="0 0 34 34"><path d="M17 2c-5.2 0-9.5 4.2-9.5 9.5 0 7.1 9.5 20.5 9.5 20.5s9.5-13.4 9.5-20.5C26.5 6.2 22.2 2 17 2z" fill="#2563eb" stroke="white" stroke-width="2"/><circle cx="17" cy="11.5" r="3.5" fill="white"/></svg>',
  )}`;
}

export function PublicNetworkMapGoogle({ markers, selectedId, zoomToId, onZoomed, searchLocation, onSelect, onRequestReferral }: PublicNetworkMapProps) {
  const isLoaded     = useGoogleMapsScript();
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef       = useRef<google.maps.Map | null>(null);
  const markerRefs   = useRef<Map<string, google.maps.Marker>>(new Map());
  const searchMarkerRef = useRef<google.maps.Marker | null>(null);
  const infoRef      = useRef<google.maps.InfoWindow | null>(null);
  const prevBoundsKey = useRef('');

  useEffect(() => {
    if (!isLoaded || !containerRef.current || mapRef.current) return;
    const map = new window.google.maps.Map(containerRef.current, {
      center: US_CENTER, zoom: 4,
      gestureHandling: 'greedy',
      fullscreenControl: false, mapTypeControl: false,
    });
    infoRef.current = new window.google.maps.InfoWindow();
    mapRef.current = map;
  }, [isLoaded]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !isLoaded) return;

    // Fit bounds or center on single marker
    const boundsKey = `${searchLocation?.label ?? ''}:${markers.map(m => m.id).join(',')}`;
    if (boundsKey !== prevBoundsKey.current) {
      prevBoundsKey.current = boundsKey;
      if (markers.length === 0 && searchLocation) {
        map.setCenter({ lat: searchLocation.latitude, lng: searchLocation.longitude });
        map.setZoom(11);
      } else if (markers.length === 1 && !searchLocation) {
        map.setCenter({ lat: markers[0].latitude, lng: markers[0].longitude });
        map.setZoom(12);
      } else if (markers.length > 0) {
        const bounds = new window.google.maps.LatLngBounds();
        markers.forEach(m => bounds.extend({ lat: m.latitude, lng: m.longitude }));
        if (searchLocation) bounds.extend({ lat: searchLocation.latitude, lng: searchLocation.longitude });
        map.fitBounds(bounds, 40);
      }
    }

    if (searchLocation) {
      const icon = {
        url: searchPinUrl(),
        scaledSize: new window.google.maps.Size(34, 34),
        anchor: new window.google.maps.Point(17, 34),
      };
      if (!searchMarkerRef.current) {
        searchMarkerRef.current = new window.google.maps.Marker({
          position: { lat: searchLocation.latitude, lng: searchLocation.longitude },
          map,
          icon,
          zIndex: 2000,
        });
      } else {
        searchMarkerRef.current.setPosition({ lat: searchLocation.latitude, lng: searchLocation.longitude });
        searchMarkerRef.current.setMap(map);
        searchMarkerRef.current.setIcon(icon);
      }
      searchMarkerRef.current.setTitle(`Search location: ${searchLocation.label}`);
    } else if (searchMarkerRef.current) {
      searchMarkerRef.current.setMap(null);
    }

    // Animate to provider if zoomToId is set (e.g. left panel click)
    if (typeof zoomToId === 'string' && zoomToId) {
      const m = markers.find(mk => mk.id === zoomToId);
      if (m) {
        map.panTo({ lat: m.latitude, lng: m.longitude });
        const currentZoom = map.getZoom() ?? 0;
        if (currentZoom < 13) map.setZoom(13);
        onZoomed?.();
      }
    }

    // Marker management
    const seen = new Set<string>();
    for (const m of markers) {
      seen.add(m.id);
      const selected = m.id === selectedId;
      const size = selected ? 34 : 28;
      const icon = { url: numberedPinUrl(m.index, m.acceptingReferrals, selected), scaledSize: new window.google.maps.Size(size, size), anchor: new window.google.maps.Point(size / 2, size / 2) };

      let marker = markerRefs.current.get(m.id);
      if (marker) {
        window.google.maps.event.clearInstanceListeners(marker);
        marker.setMap(null);
        markerRefs.current.delete(m.id);
        marker = undefined;
      }
      if (!marker) {
        marker = new window.google.maps.Marker({ position: { lat: m.latitude, lng: m.longitude }, map, icon, zIndex: selected ? 1000 : m.index });
        const captured = { ...m };
        marker.addListener('click', () => {
          map.panTo({ lat: captured.latitude, lng: captured.longitude });
          const currentZoom = map.getZoom() ?? 0;
          if (currentZoom < 13) map.setZoom(13);
          onSelect(captured.id);
          const content = `
            <div style="font-family:system-ui,sans-serif;min-width:200px">
              <div style="display:flex;align-items:center;gap:8px;margin-bottom:4px">
                <span style="width:22px;height:22px;border-radius:50%;background:${captured.acceptingReferrals ? '#dc2626' : '#6b7280'};color:#fff;display:inline-flex;align-items:center;justify-content:center;font-size:11px;font-weight:700;flex-shrink:0">${captured.index}</span>
                <p style="font-weight:700;font-size:14px;color:#111827;margin:0">${captured.name}</p>
              </div>
              ${captured.organizationName ? `<p style="font-size:12px;color:#6b7280;margin:0 0 4px">${captured.organizationName}</p>` : ''}
              <p style="font-size:12px;color:#9ca3af;margin:0 0 8px">${captured.city}, ${captured.state}</p>
              ${typeof captured.distanceMiles === 'number' ? `<p style="font-size:12px;color:#2563eb;margin:0 0 8px;font-weight:600">${captured.distanceMiles.toFixed(1)} mi away</p>` : ''}
              ${(captured.specialties ?? []).length > 0 ? `<p style="font-size:11px;color:#1d4ed8;margin:0 0 8px">${captured.specialties.map(s => s.name).join(', ')}</p>` : ''}
              ${captured.acceptingReferrals
                ? `<span style="font-size:11px;color:#15803d;background:#f0fdf4;border:1px solid #bbf7d0;border-radius:9999px;padding:2px 8px;display:inline-block;margin-bottom:10px">Accepting referrals</span>`
                : `<span style="font-size:11px;color:#6b7280;background:#f9fafb;border:1px solid #e5e7eb;border-radius:9999px;padding:2px 8px;display:inline-block;margin-bottom:10px">Not accepting referrals</span>`}
              ${captured.acceptingReferrals ? `<div><button id="gm-send-referral-${captured.id}" style="font-size:12px;color:#fff;background:#dc2626;border:none;border-radius:6px;padding:6px 14px;cursor:pointer;font-weight:600;display:block;width:100%">Send Referral</button></div>` : ''}
            </div>`;
          infoRef.current?.setContent(content);
          infoRef.current?.open({ map, anchor: marker });
          window.google.maps.event.addListenerOnce(infoRef.current!, 'domready', () => {
            document.getElementById(`gm-send-referral-${captured.id}`)?.addEventListener('click', () => {
              onRequestReferral(captured);
              infoRef.current?.close();
            });
          });
        });
        markerRefs.current.set(m.id, marker);
      }
    }

    for (const [id, marker] of markerRefs.current) {
      if (!seen.has(id)) { marker.setMap(null); markerRefs.current.delete(id); }
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [markers, selectedId, isLoaded, zoomToId, searchLocation]);

  useEffect(() => () => {
    for (const m of markerRefs.current.values()) m.setMap(null);
    markerRefs.current.clear();
    searchMarkerRef.current?.setMap(null);
    infoRef.current?.close();
  }, []);

  if (!isLoaded) {
    return <div style={{ height: '100%', width: '100%', background: '#e5e7eb', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#6b7280', fontSize: 14 }}>Loading map…</div>;
  }

  return <div ref={containerRef} style={{ height: '100%', width: '100%' }} />;
}
