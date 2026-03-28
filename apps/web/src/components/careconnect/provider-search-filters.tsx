'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useState, useCallback } from 'react';

/**
 * Provider search filter bar — client component.
 * Reads from / writes to URL search params so filters survive navigation
 * and the server component re-renders with new results.
 */
export function ProviderSearchFilters() {
  const router       = useRouter();
  const searchParams = useSearchParams();

  const [name,               setName]               = useState(searchParams.get('name')               ?? '');
  const [city,               setCity]               = useState(searchParams.get('city')               ?? '');
  const [state,              setState]              = useState(searchParams.get('state')              ?? '');
  const [categoryCode,       setCategoryCode]       = useState(searchParams.get('categoryCode')       ?? '');
  const [acceptingReferrals, setAcceptingReferrals] = useState(searchParams.get('acceptingReferrals') === 'true');

  const applyFilters = useCallback(() => {
    const params = new URLSearchParams();
    if (name)               params.set('name',               name);
    if (city)               params.set('city',               city);
    if (state)              params.set('state',              state);
    if (categoryCode)       params.set('categoryCode',       categoryCode);
    if (acceptingReferrals) params.set('acceptingReferrals', 'true');
    router.push(`/careconnect/providers?${params.toString()}`);
  }, [name, city, state, categoryCode, acceptingReferrals, router]);

  const clearFilters = useCallback(() => {
    setName('');
    setCity('');
    setState('');
    setCategoryCode('');
    setAcceptingReferrals(false);
    router.push('/careconnect/providers');
  }, [router]);

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-4">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        {/* Name / keyword */}
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Name</label>
          <input
            type="text"
            value={name}
            onChange={e => setName(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && applyFilters()}
            placeholder="Search providers…"
            className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>

        {/* City */}
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">City</label>
          <input
            type="text"
            value={city}
            onChange={e => setCity(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && applyFilters()}
            placeholder="e.g. Chicago"
            className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>

        {/* State */}
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">State</label>
          <input
            type="text"
            value={state}
            onChange={e => setState(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && applyFilters()}
            placeholder="e.g. IL"
            maxLength={2}
            className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>

        {/* Category */}
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Category</label>
          <input
            type="text"
            value={categoryCode}
            onChange={e => setCategoryCode(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && applyFilters()}
            placeholder="Category code…"
            className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>
      </div>

      {/* Accepting referrals toggle + actions */}
      <div className="mt-3 flex items-center justify-between flex-wrap gap-2">
        <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
          <input
            type="checkbox"
            checked={acceptingReferrals}
            onChange={e => setAcceptingReferrals(e.target.checked)}
            className="rounded border-gray-300 text-primary focus:ring-primary"
          />
          Accepting referrals only
        </label>

        <div className="flex items-center gap-2">
          <button
            onClick={clearFilters}
            className="text-sm text-gray-500 hover:text-gray-700 transition-colors"
          >
            Clear
          </button>
          <button
            onClick={applyFilters}
            className="bg-primary text-white text-sm font-medium px-4 py-1.5 rounded-md hover:opacity-90 transition-opacity"
          >
            Search
          </button>
        </div>
      </div>
    </div>
  );
}
