'use client';

import { useState } from 'react';
import { BaseSelect } from '@/components/ui/base-select';
import { cn } from '@/lib/utils';

interface FilterOption {
  value: string;
  label: string;
}

interface FilterToolbarProps {
  searchPlaceholder?: string;
  filters?: { label: string; options: FilterOption[]; value: string; onChange: (v: string) => void }[];
  onSearch?: (query: string) => void;
  searchValue?: string;
  children?: React.ReactNode;
  onSearchFocus?: () => void;
  onSearchBlur?: () => void;
  dropdown?: React.ReactNode;
  /** Skip the outer card chrome (background/border/rounding) — for embedding inside another bordered container, e.g. BaseTable's `toolbar` slot. */
  bare?: boolean;
}

export function FilterToolbar({ searchPlaceholder = 'Search...', filters, onSearch, searchValue = '', children, onSearchFocus, onSearchBlur, dropdown, bare }: FilterToolbarProps) {
  const [query, setQuery] = useState(searchValue);

  return (
    <div
      className={cn(
        'flex flex-wrap items-center gap-3',
        bare ? 'px-4 py-3 border-b border-gray-100' : 'bg-white border border-gray-200 rounded-xl px-4 py-3',
      )}
    >
      <div className="relative flex-1 min-w-[200px]">
        <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
        <input
          type="text"
          placeholder={searchPlaceholder}
          value={query}
          onChange={(e) => { setQuery(e.target.value); onSearch?.(e.target.value); }}
          onFocus={onSearchFocus}
          onBlur={onSearchBlur}
          className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
        />
        {dropdown}
      </div>
      {filters?.map((filter, i) => (
        <BaseSelect
          key={i}
          value={filter.value}
          onChange={(v) => filter.onChange(v)}
          options={filter.options}
          placeholder={filter.label}
          clearable
          className="w-auto min-w-[130px]"
        />
      ))}
      {children}
    </div>
  );
}
