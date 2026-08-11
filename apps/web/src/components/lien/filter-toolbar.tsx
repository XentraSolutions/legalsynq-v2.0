'use client';

import { useState } from 'react';
import { Search, X } from 'lucide-react';
import { BaseSelect } from '@/components/ui/base-select';
import { buttonVariants } from '@/components/ui/button';
import { cn } from '@/lib/utils';

interface FilterOption {
  value: string;
  label: string;
}

interface ToolbarFilter {
  label: string;
  options: FilterOption[];
  value: string;
  onChange: (v: string) => void;
  /**
   * Renders the trigger as an icon + label pill (matching the app's
   * "Filter" action button) that switches to showing the selected option's
   * label plus a clear control once something's picked — same select
   * behavior underneath, just a different trigger look than the default
   * combobox chrome.
   */
  icon?: React.ReactNode;
}

interface FilterToolbarProps {
  searchPlaceholder?: string;
  filters?: ToolbarFilter[];
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
      <div className="relative flex-1 min-w-[200px] max-w-md">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
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
          className={filter.icon ? cn(buttonVariants({ variant: 'secondary' }), 'border-gray-300') : 'w-auto min-w-[130px]'}
          contentClassName={filter.icon ? 'w-56' : undefined}
          triggerContent={
            filter.icon
              ? ({ selectedLabel, onClear }) => (
                  <>
                    {filter.icon}
                    <span className="truncate max-w-[140px]">{selectedLabel || filter.label}</span>
                    {selectedLabel && (
                      <X
                        aria-label="Clear selection"
                        className="h-3.5 w-3.5 text-gray-400 hover:text-gray-600"
                        onClick={onClear}
                      />
                    )}
                  </>
                )
              : undefined
          }
        />
      ))}
      {children}
    </div>
  );
}
