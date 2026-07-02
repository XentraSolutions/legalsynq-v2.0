'use client';

import { useState, useTransition } from 'react';
import { tenantClientApi } from '@/lib/tenant-client-api';
import { googleMapsKey, type MapProvider } from '@/lib/use-map-provider';

const hasKey = !!googleMapsKey();

export function TenantMapProviderCard({
  tenantId,
  initialProvider,
}: {
  tenantId:        string;
  initialProvider: MapProvider;
}) {
  const [selected, setSelected] = useState<MapProvider>(initialProvider);
  const [saved,    setSaved]    = useState<MapProvider>(initialProvider);
  const [error,    setError]    = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const isDirty = selected !== saved;

  const save = () => {
    setError(null);
    startTransition(async () => {
      try {
        await tenantClientApi.upsertMapProviderSetting(tenantId, selected);
        setSaved(selected);
      } catch {
        setError('Failed to save map provider. Please try again.');
      }
    });
  };

  return (
    <div className="bg-white border border-gray-200 rounded-xl px-6 py-6">
      <div className="mb-5">
        <h2 className="text-sm font-semibold text-gray-900 flex items-center gap-2">
          <i className="ri-map-2-line text-gray-400" />
          Map provider
        </h2>
        <p className="text-xs text-gray-500 mt-1">
          Choose which map engine is used across CareConnect for all users in your tenant.
        </p>
      </div>

      <div className="flex flex-col gap-3">
        <MapOption
          id="google"
          label="Google Maps"
          description={
            hasKey
              ? 'Google Maps Platform — richer satellite imagery, Street View, and real-time traffic.'
              : 'Requires a Google Maps API key (NEXT_PUBLIC_GOOGLE_MAPS_KEY). Contact your platform administrator.'
          }
          icon="ri-google-fill"
          current={selected}
          onSelect={setSelected}
          disabled={!hasKey}
        />
        <MapOption
          id="osm"
          label="OpenStreetMap"
          description="Free, open-source map tiles — no API key required."
          icon="ri-map-line"
          current={selected}
          onSelect={setSelected}
          disabled={false}
        />
      </div>

      {!hasKey && (
        <div className="mt-4 rounded-lg bg-amber-50 border border-amber-200 px-4 py-3">
          <p className="text-xs font-semibold text-amber-800 mb-0.5">Google Maps key not configured</p>
          <p className="text-xs text-amber-700">
            Set <code className="font-mono bg-amber-100 px-1 rounded">NEXT_PUBLIC_GOOGLE_MAPS_KEY</code> to
            enable Google Maps for your tenant.
          </p>
        </div>
      )}

      {error && (
        <div className="mt-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3">
          <p className="text-xs text-red-700">{error}</p>
        </div>
      )}

      <div className="mt-5 flex items-center justify-between">
        <p className="text-xs text-gray-400">
          {isDirty
            ? 'Unsaved changes — click Save to apply to all tenant users.'
            : saved === 'google'
              ? 'Google Maps is active for all users.'
              : 'OpenStreetMap is active for all users.'}
        </p>
        <button
          type="button"
          disabled={!isDirty || isPending}
          onClick={save}
          className="inline-flex items-center gap-1.5 px-4 py-2 text-xs font-semibold rounded-lg bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {isPending ? (
            <>
              <i className="ri-loader-4-line animate-spin" />
              Saving…
            </>
          ) : (
            <>
              <i className="ri-save-line" />
              Save
            </>
          )}
        </button>
      </div>
    </div>
  );
}

function MapOption({
  id, label, description, icon, current, onSelect, disabled,
}: {
  id:          MapProvider;
  label:       string;
  description: string;
  icon:        string;
  current:     MapProvider;
  onSelect:    (p: MapProvider) => void;
  disabled:    boolean;
}) {
  const active = current === id && !disabled;

  return (
    <button
      type="button"
      disabled={disabled}
      onClick={() => !disabled && onSelect(id)}
      className={[
        'w-full text-left flex items-start gap-3 rounded-lg border px-4 py-3 transition-all',
        disabled
          ? 'opacity-50 cursor-not-allowed border-gray-200 bg-gray-50'
          : active
            ? 'border-blue-500 bg-blue-50 ring-1 ring-blue-500'
            : 'border-gray-200 bg-white hover:border-gray-300 hover:bg-gray-50 cursor-pointer',
      ].join(' ')}
    >
      <div className={`mt-0.5 w-5 h-5 rounded-full border-2 flex items-center justify-center shrink-0 ${active ? 'border-blue-500 bg-blue-500' : 'border-gray-300 bg-white'}`}>
        {active && <div className="w-2 h-2 rounded-full bg-white" />}
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1.5">
          <i className={`${icon} text-gray-500 text-sm`} />
          <span className="text-sm font-medium text-gray-900">{label}</span>
          {active && (
            <span className="ml-auto text-[10px] font-semibold text-blue-600 bg-blue-100 px-2 py-0.5 rounded-full">
              Active
            </span>
          )}
        </div>
        <p className="text-xs text-gray-500 mt-0.5 leading-relaxed">{description}</p>
      </div>
    </button>
  );
}
