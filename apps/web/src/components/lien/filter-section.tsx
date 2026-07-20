'use client';

import { useState, type ReactNode } from 'react';
import { BaseSelect } from '@/components/ui/base-select';
import type { InfiniteOptions } from '@/hooks/use-filter-options';

/**
 * Label + "Select All" header above an inline BaseSelect list. Pages load in
 * the background as soon as the list is enabled, so by the time someone
 * clicks Select All it's normally already the complete list; `loadAll` is
 * the fallback for the rare case that background load hasn't finished yet.
 */
export function FilterSection({
  label,
  source,
  selected,
  onChange,
  children,
}: {
  label: string;
  source: InfiniteOptions;
  selected: string[];
  onChange: (values: string[]) => void;
  children: ReactNode;
}) {
  const [selecting, setSelecting] = useState(false);

  // Only "all selected" once the full list is loaded and every option in it
  // is selected — a partial/still-loading list must never read as complete.
  const allSelected =
    source.allLoaded &&
    source.options.length > 0 &&
    source.options.every((o) => selected.includes(o.value));

  const selectAll = async () => {
    if (allSelected) {
      const optionValues = new Set(source.options.map((o) => o.value));
      onChange(selected.filter((v) => !optionValues.has(v)));
      return;
    }
    if (source.allLoaded) {
      onChange(Array.from(new Set([...selected, ...source.options.map((o) => o.value)])));
      return;
    }
    setSelecting(true);
    try {
      const all = await source.loadAll();
      onChange(Array.from(new Set([...selected, ...all.map((o) => o.value)])));
    } finally {
      setSelecting(false);
    }
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-1">
        <span className="block text-sm font-medium text-gray-700">{label}</span>
        <button
          type="button"
          onClick={selectAll}
          disabled={selecting}
          className="text-xs font-medium text-primary hover:underline disabled:opacity-50"
        >
          {selecting ? 'Loading…' : allSelected ? 'Unselect All' : 'Select All'}
        </button>
      </div>
      {children}
    </div>
  );
}

/**
 * Inline checkbox list backed by a scroll-paginated option source. Every
 * page is background-loaded before this ever renders (see
 * useBackgroundInfiniteOptions), so search is plain client-side filtering
 * over whatever's already loaded — no server round-trip per keystroke,
 * `loadingMode="infinite"` here only covers the (usually already-finished)
 * background page loading, not searching.
 */
export function InfiniteFilterList({
  source,
  searchPlaceholder,
  selected,
  onChange,
}: {
  source: InfiniteOptions;
  searchPlaceholder: string;
  selected: string[];
  onChange: (values: string[]) => void;
}) {
  return (
    <BaseSelect
      multiple
      inline
      showCheckboxes
      options={source.options}
      value={selected}
      onChange={(values) => onChange(values)}
      loadingMode="infinite"
      isLoading={source.isLoading}
      isFetchingMore={source.isFetchingMore}
      hasNextPage={source.hasNextPage}
      onLoadMore={source.loadMore}
      searchPlaceholder={searchPlaceholder}
      emptyText="No results found"
    />
  );
}
