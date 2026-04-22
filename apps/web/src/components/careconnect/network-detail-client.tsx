'use client';

import dynamic from 'next/dynamic';
import { useState } from 'react';
import { careConnectApi } from '@/lib/careconnect-api';
import type { NetworkDetail, NetworkProviderMarker, ProviderMarker } from '@/types/careconnect';

const ProviderMap = dynamic(
  () => import('./provider-map').then(m => m.ProviderMap),
  { ssr: false, loading: () => <div className="h-80 w-full bg-gray-100 animate-pulse rounded-lg" /> },
);

/** Adapt NetworkProviderMarker to the ProviderMap's ProviderMarker contract. */
function toProviderMarker(m: NetworkProviderMarker): ProviderMarker {
  return {
    ...m,
    displayLabel:    m.organizationName ?? m.name,
    markerSubtitle:  `${m.city}, ${m.state}`,
    primaryCategory: undefined,
    categories:      [],
  };
}

interface NetworkDetailClientProps {
  network:        NetworkDetail;
  initialMarkers: NetworkProviderMarker[];
}

export function NetworkDetailClient({ network, initialMarkers }: NetworkDetailClientProps) {
  const [providers, setProviders] = useState(network.providers);
  const [markers, setMarkers] = useState<NetworkProviderMarker[]>(initialMarkers);
  const [searchProviderId, setSearchProviderId] = useState('');
  const [adding, setAdding] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);
  const [removingId, setRemovingId] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'providers' | 'map'>('providers');

  async function handleAddProvider(e: React.FormEvent) {
    e.preventDefault();
    const pid = searchProviderId.trim();
    if (!pid) {
      setAddError('Enter a Provider ID (UUID).');
      return;
    }
    setAdding(true);
    setAddError(null);
    try {
      await careConnectApi.networks.addProvider(network.id, pid);
      // Reload to get the updated provider list and markers from the server
      window.location.reload();
    } catch {
      setAddError('Failed to add provider. Check the ID and try again.');
    } finally {
      setAdding(false);
    }
  }

  async function handleRemoveProvider(providerId: string) {
    if (!confirm('Remove this provider from the network?')) return;
    setRemovingId(providerId);
    try {
      await careConnectApi.networks.removeProvider(network.id, providerId);
      setProviders(prev => prev.filter(p => p.id !== providerId));
      setMarkers(prev => prev.filter(m => m.id !== providerId));
    } catch {
      alert('Failed to remove provider. Please try again.');
    } finally {
      setRemovingId(null);
    }
  }

  const providerMarkers = markers.map(toProviderMarker);

  return (
    <div>
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-semibold text-gray-900">{network.name}</h1>
        {network.description && (
          <p className="text-sm text-gray-500 mt-1">{network.description}</p>
        )}
        <p className="text-xs text-gray-400 mt-1">
          {providers.length} provider{providers.length === 1 ? '' : 's'}
        </p>
      </div>

      {/* Add Provider */}
      <div className="mb-6 rounded-lg border border-gray-200 bg-gray-50 p-4">
        <h2 className="text-sm font-semibold text-gray-700 mb-2">Add Provider</h2>
        <form onSubmit={handleAddProvider} className="flex gap-2 items-start">
          <div className="flex-1">
            <input
              type="text"
              value={searchProviderId}
              onChange={e => setSearchProviderId(e.target.value)}
              placeholder="Paste Provider ID (UUID)"
              className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
            />
            {addError && <p className="text-xs text-red-600 mt-1">{addError}</p>}
          </div>
          <button
            type="submit"
            disabled={adding}
            className="rounded-md bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 whitespace-nowrap"
          >
            {adding ? 'Adding…' : 'Add Provider'}
          </button>
        </form>
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-200 mb-4">
        <div className="flex gap-4">
          {(['providers', 'map'] as const).map(tab => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`pb-2 text-sm font-medium capitalize border-b-2 transition-colors ${
                activeTab === tab
                  ? 'border-blue-600 text-blue-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              {tab === 'providers' ? `Providers (${providers.length})` : 'Map'}
            </button>
          ))}
        </div>
      </div>

      {/* Providers tab */}
      {activeTab === 'providers' && (
        providers.length === 0 ? (
          <div className="rounded-lg border-2 border-dashed border-gray-200 py-12 text-center">
            <i className="ri-hospital-line text-3xl text-gray-300" />
            <p className="mt-2 text-sm text-gray-500">No providers in this network yet.</p>
          </div>
        ) : (
          <div className="divide-y divide-gray-100 rounded-lg border border-gray-200 bg-white overflow-hidden">
            {providers.map(provider => (
              <div key={provider.id} className="flex items-center justify-between px-4 py-3 hover:bg-gray-50">
                <div className="min-w-0 flex-1">
                  <p className="font-medium text-gray-900 truncate">{provider.name}</p>
                  {provider.organizationName && (
                    <p className="text-sm text-gray-500 truncate">{provider.organizationName}</p>
                  )}
                  <p className="text-xs text-gray-400">
                    {provider.city}, {provider.state} · {provider.email}
                  </p>
                </div>
                <div className="flex items-center gap-3 ml-4">
                  <span className={`text-xs font-medium px-2 py-0.5 rounded-full border ${
                    provider.acceptingReferrals
                      ? 'bg-green-50 text-green-700 border-green-200'
                      : 'bg-gray-50 text-gray-500 border-gray-200'
                  }`}>
                    {provider.acceptingReferrals ? 'Accepting' : 'Not accepting'}
                  </span>
                  <button
                    onClick={() => handleRemoveProvider(provider.id)}
                    disabled={removingId === provider.id}
                    className="text-xs text-red-500 hover:text-red-700 disabled:opacity-40"
                    title="Remove from network"
                  >
                    <i className="ri-close-circle-line text-base" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )
      )}

      {/* Map tab */}
      {activeTab === 'map' && (
        <div className="h-96 rounded-lg overflow-hidden border border-gray-200">
          {providerMarkers.length === 0 ? (
            <div className="h-full flex items-center justify-center bg-gray-50">
              <p className="text-sm text-gray-400">No geocoded providers in this network.</p>
            </div>
          ) : (
            <ProviderMap
            markers={providerMarkers}
            selectedId={null}
            onSelect={() => {}}
            onViewportChange={() => {}}
            isReferrer={false}
          />
          )}
        </div>
      )}
    </div>
  );
}
