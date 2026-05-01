'use client';

/**
 * CC-REFERRER-BROWSE — Read-only provider network directory for law firm users.
 *
 * Layout:
 *   Left panel  : scrollable list of network cards
 *   Right panel : provider map + provider list for the selected network
 *
 * Markers are fetched client-side when the user selects a network card.
 */

import { useState, useCallback, useEffect } from 'react';
import dynamic from 'next/dynamic';
import type { NetworkSummary, NetworkProviderMarker } from '@/types/careconnect';

const MyNetworkMap = dynamic(
  () => import('./my-network-map').then(m => m.MyNetworkMap),
  { ssr: false, loading: () => <div className="h-full w-full bg-gray-100 animate-pulse rounded-lg" /> },
);

interface BrowseNetworksClientProps {
  initialNetworks: NetworkSummary[];
  fetchError:      string | null;
}

export function BrowseNetworksClient({ initialNetworks, fetchError }: BrowseNetworksClientProps) {
  const [selectedNetwork, setSelectedNetwork] = useState<NetworkSummary | null>(
    initialNetworks.length === 1 ? initialNetworks[0] : null,
  );
  const [markers,         setMarkers]         = useState<NetworkProviderMarker[]>([]);
  const [selectedMarkerId, setSelectedMarkerId] = useState<string | null>(null);
  const [loadingMarkers,  setLoadingMarkers]  = useState(false);
  const [markerError,     setMarkerError]     = useState<string | null>(null);

  const loadMarkers = useCallback(async (networkId: string) => {
    setLoadingMarkers(true);
    setMarkerError(null);
    setMarkers([]);
    setSelectedMarkerId(null);
    try {
      const res = await fetch(`/careconnect/api/networks/directory/${networkId}/markers`, {
        credentials: 'include',
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data: NetworkProviderMarker[] = await res.json();
      setMarkers(data);
    } catch {
      setMarkerError('Could not load providers for this network.');
    } finally {
      setLoadingMarkers(false);
    }
  }, []);

  useEffect(() => {
    if (selectedNetwork) loadMarkers(selectedNetwork.id);
  }, [selectedNetwork, loadMarkers]);

  if (fetchError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6 text-sm text-red-700">
        {fetchError}
      </div>
    );
  }

  if (initialNetworks.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-24 text-center">
        <i className="ri-share-circle-line text-4xl text-gray-300 mb-3" />
        <p className="text-sm font-medium text-gray-500">No provider networks are available yet.</p>
        <p className="text-xs text-gray-400 mt-1">Check back later or contact your coordinator.</p>
      </div>
    );
  }

  const selectedMarker = markers.find(m => m.id === selectedMarkerId) ?? null;

  return (
    <div className="flex h-[calc(100vh-10rem)] gap-4 overflow-hidden">

      {/* ── Left: network list ──────────────────────────────────────────── */}
      <aside className="w-72 shrink-0 overflow-y-auto space-y-2 pr-1">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-400 px-1 mb-3">
          Provider Networks
        </h2>
        {initialNetworks.map(network => {
          const active = selectedNetwork?.id === network.id;
          return (
            <button
              key={network.id}
              onClick={() => setSelectedNetwork(network)}
              className={[
                'w-full text-left rounded-lg border px-4 py-3 transition-colors',
                active
                  ? 'border-blue-500 bg-blue-50 ring-1 ring-blue-400'
                  : 'border-gray-200 bg-white hover:border-gray-300 hover:bg-gray-50',
              ].join(' ')}
            >
              <p className={['text-sm font-medium', active ? 'text-blue-700' : 'text-gray-800'].join(' ')}>
                {network.name}
              </p>
              {network.description && (
                <p className="mt-0.5 text-xs text-gray-500 line-clamp-2">{network.description}</p>
              )}
              <p className="mt-1.5 text-xs text-gray-400">
                <i className="ri-hospital-line mr-1" />
                {network.providerCount} provider{network.providerCount !== 1 ? 's' : ''}
              </p>
            </button>
          );
        })}
      </aside>

      {/* ── Right: map + provider detail ───────────────────────────────── */}
      <div className="flex flex-1 flex-col gap-3 overflow-hidden min-w-0">

        {!selectedNetwork ? (
          <div className="flex flex-1 items-center justify-center rounded-lg border border-dashed border-gray-200 bg-gray-50">
            <div className="text-center">
              <i className="ri-map-pin-2-line text-3xl text-gray-300 mb-2 block" />
              <p className="text-sm text-gray-500">Select a network to view its providers</p>
            </div>
          </div>
        ) : (
          <>
            {/* Network header */}
            <div className="shrink-0">
              <h1 className="text-base font-semibold text-gray-900">{selectedNetwork.name}</h1>
              {selectedNetwork.description && (
                <p className="text-sm text-gray-500 mt-0.5">{selectedNetwork.description}</p>
              )}
            </div>

            {/* Map */}
            <div className="relative flex-1 rounded-lg overflow-hidden border border-gray-200 min-h-0">
              {loadingMarkers ? (
                <div className="absolute inset-0 flex items-center justify-center bg-gray-50">
                  <div className="flex items-center gap-2 text-sm text-gray-500">
                    <i className="ri-loader-4-line animate-spin" />
                    Loading providers…
                  </div>
                </div>
              ) : markerError ? (
                <div className="absolute inset-0 flex items-center justify-center bg-red-50">
                  <p className="text-sm text-red-600">{markerError}</p>
                </div>
              ) : (
                <MyNetworkMap
                  markers={markers}
                  selectedId={selectedMarkerId}
                  onSelect={setSelectedMarkerId}
                />
              )}
            </div>

            {/* Selected provider detail strip */}
            {selectedMarker && (
              <div className="shrink-0 rounded-lg border border-gray-200 bg-white px-4 py-3 flex items-start gap-4">
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-semibold text-gray-900 truncate">{selectedMarker.name}</p>
                  {selectedMarker.organizationName && (
                    <p className="text-xs text-gray-500 truncate">{selectedMarker.organizationName}</p>
                  )}
                  <p className="text-xs text-gray-500 mt-0.5">
                    {[selectedMarker.addressLine1, selectedMarker.city, selectedMarker.state, selectedMarker.postalCode]
                      .filter(Boolean).join(', ')}
                  </p>
                </div>
                <div className="shrink-0 space-y-1 text-right">
                  {selectedMarker.phone && (
                    <a href={`tel:${selectedMarker.phone}`}
                       className="block text-xs text-blue-600 hover:underline">
                      {selectedMarker.phone}
                    </a>
                  )}
                  {selectedMarker.email && (
                    <a href={`mailto:${selectedMarker.email}`}
                       className="block text-xs text-blue-600 hover:underline truncate max-w-[16rem]">
                      {selectedMarker.email}
                    </a>
                  )}
                  <span className={[
                    'inline-block text-xs font-medium px-2 py-0.5 rounded-full',
                    selectedMarker.acceptingReferrals
                      ? 'bg-green-100 text-green-700'
                      : 'bg-gray-100 text-gray-500',
                  ].join(' ')}>
                    {selectedMarker.acceptingReferrals ? 'Accepting referrals' : 'Not accepting'}
                  </span>
                </div>
              </div>
            )}

            {/* Provider count strip when nothing selected on map */}
            {!selectedMarker && !loadingMarkers && markers.length > 0 && (
              <p className="shrink-0 text-xs text-gray-400 text-center">
                {markers.length} provider{markers.length !== 1 ? 's' : ''} in this network — click a pin to see details
              </p>
            )}
          </>
        )}
      </div>
    </div>
  );
}
