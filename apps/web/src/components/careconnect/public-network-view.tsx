'use client';

/**
 * CC2-INT-B07 — Public Network View.
 *
 * Interactive client component for the public /network page.
 * Shows a searchable provider list alongside a badge indicating access stage.
 *
 * Stage enforcement (CC2-INT-B06-02):
 *  - URL           → No portal link. Provider receives referrals via signed token URLs.
 *  - COMMON_PORTAL → "Access Portal" link → redirects to /login (common portal login).
 *  - TENANT        → "Tenant Portal" link → redirects to /login (tenant portal login).
 */

import { useState, useMemo } from 'react';
import type { PublicNetworkDetail, PublicProviderItem } from '@/lib/public-network-api';

interface PublicNetworkViewProps {
  detail:     PublicNetworkDetail;
  tenantCode: string;
}

export function PublicNetworkView({ detail, tenantCode }: PublicNetworkViewProps) {
  const [search, setSearch]         = useState('');
  const [filterActive, setFilter]   = useState<'all' | 'accepting'>('accepting');

  const filtered = useMemo(() => {
    let list = detail.providers;

    if (filterActive === 'accepting') {
      list = list.filter(p => p.acceptingReferrals);
    }

    const q = search.trim().toLowerCase();
    if (q) {
      list = list.filter(p =>
        p.name.toLowerCase().includes(q) ||
        (p.organizationName?.toLowerCase().includes(q) ?? false) ||
        p.city.toLowerCase().includes(q) ||
        p.state.toLowerCase().includes(q),
      );
    }

    return list;
  }, [detail.providers, search, filterActive]);

  return (
    <div className="space-y-4">
      {/* Network header */}
      <div className="bg-white border border-gray-200 rounded-lg p-4">
        <h2 className="text-base font-semibold text-gray-900">{detail.networkName}</h2>
        {detail.networkDescription && (
          <p className="mt-1 text-sm text-gray-500">{detail.networkDescription}</p>
        )}
        <p className="mt-2 text-xs text-gray-400">
          {detail.providers.length} provider{detail.providers.length !== 1 ? 's' : ''} in this network
        </p>
      </div>

      {/* Search + filter bar */}
      <div className="flex flex-col sm:flex-row gap-3">
        <input
          type="search"
          placeholder="Search by name, city, or state…"
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="flex-1 rounded-md border border-gray-200 px-3 py-2 text-sm placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary"
        />
        <div className="flex rounded-md overflow-hidden border border-gray-200 text-sm">
          <button
            onClick={() => setFilter('accepting')}
            className={[
              'px-3 py-2 transition-colors',
              filterActive === 'accepting'
                ? 'bg-primary text-white'
                : 'bg-white text-gray-600 hover:bg-gray-50',
            ].join(' ')}
          >
            Accepting referrals
          </button>
          <button
            onClick={() => setFilter('all')}
            className={[
              'px-3 py-2 border-l border-gray-200 transition-colors',
              filterActive === 'all'
                ? 'bg-primary text-white'
                : 'bg-white text-gray-600 hover:bg-gray-50',
            ].join(' ')}
          >
            All providers
          </button>
        </div>
      </div>

      {/* Provider list */}
      {filtered.length === 0 ? (
        <p className="text-sm text-gray-500 py-8 text-center">
          No providers match your search.
        </p>
      ) : (
        <div className="space-y-3">
          {filtered.map(p => (
            <PublicProviderCard key={p.id} provider={p} />
          ))}
        </div>
      )}
    </div>
  );
}

// ── Provider card ─────────────────────────────────────────────────────────────

function PublicProviderCard({ provider }: { provider: PublicProviderItem }) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg p-4 flex items-start justify-between gap-4">
      {/* Provider details */}
      <div className="min-w-0 flex-1 space-y-1">
        <div className="flex items-center gap-2 flex-wrap">
          <p className="font-medium text-gray-900 truncate">{provider.name}</p>
          <AccessStagePill stage={provider.accessStage} />
          {provider.acceptingReferrals ? (
            <span className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-green-50 text-green-700 ring-1 ring-inset ring-green-600/20">
              Accepting referrals
            </span>
          ) : (
            <span className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-gray-100 text-gray-500 ring-1 ring-inset ring-gray-300/40">
              Not accepting
            </span>
          )}
        </div>

        {provider.organizationName && (
          <p className="text-sm text-gray-600 truncate">{provider.organizationName}</p>
        )}

        <p className="text-xs text-gray-500">
          {provider.city}, {provider.state} {provider.postalCode}
        </p>

        {provider.phone && (
          <a
            href={`tel:${provider.phone}`}
            className="text-xs text-primary hover:underline"
          >
            {provider.phone}
          </a>
        )}
      </div>

      {/* Stage-based portal action */}
      <StageAction stage={provider.accessStage} />
    </div>
  );
}

// ── Stage badge ───────────────────────────────────────────────────────────────

function AccessStagePill({ stage }: { stage: string }) {
  if (stage === 'COMMON_PORTAL') {
    return (
      <span className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-blue-50 text-blue-700 ring-1 ring-inset ring-blue-600/20">
        Portal active
      </span>
    );
  }
  if (stage === 'TENANT') {
    return (
      <span className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-purple-50 text-purple-700 ring-1 ring-inset ring-purple-600/20">
        Tenant portal
      </span>
    );
  }
  return null; // URL stage — no badge
}

// ── Stage-based action ────────────────────────────────────────────────────────

/**
 * CC2-INT-B07 stage routing.
 *
 * COMMON_PORTAL → link to /login (common portal authentication)
 * TENANT        → link to /login (tenant portal — same gate, different post-login redirect)
 * URL           → no action (referrals come via signed token URLs from the law firm)
 */
function StageAction({ stage }: { stage: string }) {
  if (stage === 'COMMON_PORTAL') {
    return (
      <a
        href="/login"
        className="shrink-0 text-xs font-medium text-primary hover:underline"
        title="This provider has activated their portal account"
      >
        View portal
      </a>
    );
  }
  if (stage === 'TENANT') {
    return (
      <a
        href="/login"
        className="shrink-0 text-xs font-medium text-purple-600 hover:underline"
        title="This provider has a dedicated tenant portal"
      >
        Tenant portal
      </a>
    );
  }
  return null; // URL stage — no portal link
}
