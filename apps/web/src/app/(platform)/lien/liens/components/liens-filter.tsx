'use client';

import { useEffect, useState, type ReactNode } from 'react';
import { FormModal } from '@/components/lien/modal';
import { DatePicker } from '@/components/ui/date-picker';
import { BaseSelect } from '@/components/ui/base-select';
import {
  useInfiniteContactOptions,
  useInfiniteCaseManagerOptions,
  useLienStatusOptions,
  type InfiniteOptions,
} from '@/hooks/use-filter-options';

export interface LiensFilterValues {
  lawFirmIds: string[];
  medicalFacilityIds: string[];
  caseManagerIds: string[];
  lienStatusIds: string[];
  purchaseDateFrom: string;
  purchaseDateTo: string;
  closedDateFrom: string;
  closedDateTo: string;
}

function parseLocalDate(value: string): Date | undefined {
  if (!value) return undefined;
  const d = new Date(value + 'T00:00:00');
  return isNaN(d.getTime()) ? undefined : d;
}

export const EMPTY_LIENS_FILTERS: LiensFilterValues = {
  lawFirmIds: [],
  medicalFacilityIds: [],
  caseManagerIds: [],
  lienStatusIds: [],
  purchaseDateFrom: '',
  purchaseDateTo: '',
  closedDateFrom: '',
  closedDateTo: '',
};

interface LiensFilterProps {
  open: boolean;
  onClose: () => void;
  value: LiensFilterValues;
  onApplyFilter: (filters: LiensFilterValues) => void;
  /**
   * From `useBackgroundReady()` — true once the page's primary content
   * (the liens table) has finished loading. Option lists then start
   * prefetching in the background even before the modal opens, so by the
   * time someone clicks Filter the lists are usually already populated.
   */
  primaryReady?: boolean;
}

/**
 * Label + "Select All" header above an inline BaseSelect list. Pages load in
 * the background as soon as the list is enabled, so by the time someone
 * clicks Select All it's normally already the complete list; `loadAll` is
 * the fallback for the rare case that background load hasn't finished yet.
 */
function FilterSection({
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

  const selectAll = async () => {
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
          {selecting ? 'Loading…' : 'Select All'}
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
function InfiniteFilterList({
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

export function LiensFilter({ open, onClose, value, onApplyFilter, primaryReady }: LiensFilterProps) {
  const [draft, setDraft] = useState<LiensFilterValues>(value);

  // Enabled once the modal is open OR the page's primary content is ready —
  // whichever happens first — so these lists are usually already warm by
  // the time someone opens the modal instead of starting cold right then.
  const listsEnabled = open || !!primaryReady;

  const lawFirms = useInfiniteContactOptions('LawFirm', { enabled: listsEnabled });
  // mainOnly: facility staff sub-contacts don't belong in this filter list.
  const facilities = useInfiniteContactOptions('MedicalFacility', {
    enabled: listsEnabled,
    mainOnly: true,
  });
  const caseManagers = useInfiniteCaseManagerOptions({ enabled: listsEnabled });
  const statuses = useLienStatusOptions({ enabled: listsEnabled });

  // Re-sync the draft with the page's active filters every time the modal opens.
  useEffect(() => {
    if (open) setDraft(value);
  }, [open, value]);

  const handleSubmit = () => {
    onApplyFilter(draft);
    onClose();
  };

  const handleClear = () => setDraft(EMPTY_LIENS_FILTERS);

  const handleClose = () => {
    setDraft(value);
    onClose();
  };

  return (
    <FormModal
      open={open}
      onClose={handleClose}
      onSubmit={handleSubmit}
      title="Filter Liens"
      subtitle="Narrow down liens using filters to quickly find relevant results."
      submitLabel="Apply Filters"
      size="lg"
      headerActions={
        <button
          type="button"
          onClick={handleClear}
          className="flex items-center gap-1.5 text-xs font-medium text-primary border border-primary/30 rounded-lg px-3 py-1.5 hover:bg-primary/5 transition-colors"
        >
          <i className="ri-refresh-line text-sm" />
          Clear Filter
        </button>
      }
    >
      <div className="space-y-5">
        <div className="grid grid-cols-2 gap-4">
          <FilterSection
            label="Law Firm"
            source={lawFirms}
            selected={draft.lawFirmIds}
            onChange={(v) => setDraft({ ...draft, lawFirmIds: v })}
          >
            <InfiniteFilterList
              source={lawFirms}
              searchPlaceholder="Search Law Firm…"
              selected={draft.lawFirmIds}
              onChange={(v) => setDraft({ ...draft, lawFirmIds: v })}
            />
          </FilterSection>
          <FilterSection
            label="Medical Facility"
            source={facilities}
            selected={draft.medicalFacilityIds}
            onChange={(v) => setDraft({ ...draft, medicalFacilityIds: v })}
          >
            <InfiniteFilterList
              source={facilities}
              searchPlaceholder="Search Medical Facility…"
              selected={draft.medicalFacilityIds}
              onChange={(v) => setDraft({ ...draft, medicalFacilityIds: v })}
            />
          </FilterSection>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <FilterSection
            label="Case Manager"
            source={caseManagers}
            selected={draft.caseManagerIds}
            onChange={(v) => setDraft({ ...draft, caseManagerIds: v })}
          >
            <InfiniteFilterList
              source={caseManagers}
              searchPlaceholder="Search Case Manager…"
              selected={draft.caseManagerIds}
              onChange={(v) => setDraft({ ...draft, caseManagerIds: v })}
            />
          </FilterSection>
          <FilterSection
            label="Liens Status"
            source={statuses}
            selected={draft.lienStatusIds}
            onChange={(v) => setDraft({ ...draft, lienStatusIds: v })}
          >
            <BaseSelect
              multiple
              inline
              showCheckboxes
              options={statuses.options}
              value={draft.lienStatusIds}
              onChange={(values) => setDraft({ ...draft, lienStatusIds: values })}
              isLoading={statuses.isLoading}
              searchPlaceholder="Search Lien Status…"
              emptyText="No results found"
            />
          </FilterSection>
        </div>
        <div>
          <p className="text-sm font-medium text-gray-700 mb-2">Purchase Date</p>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-gray-500 mb-1">From Date</label>
              <DatePicker
                value={draft.purchaseDateFrom}
                onChange={(v) => setDraft({ ...draft, purchaseDateFrom: v })}
                // Can't be after the To date already picked (also implies
                // "not in the future", since To is itself capped at today).
                maxDate={parseLocalDate(draft.purchaseDateTo)}
                disableFutureDates
              />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">To Date</label>
              <DatePicker
                value={draft.purchaseDateTo}
                onChange={(v) => setDraft({ ...draft, purchaseDateTo: v })}
                minDate={parseLocalDate(draft.purchaseDateFrom)}
                disableFutureDates
              />
            </div>
          </div>
        </div>
        <div>
          <p className="text-sm font-medium text-gray-700 mb-2">Closed Date</p>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-gray-500 mb-1">From Date</label>
              <DatePicker
                value={draft.closedDateFrom}
                onChange={(v) => setDraft({ ...draft, closedDateFrom: v })}
                maxDate={parseLocalDate(draft.closedDateTo)}
                disableFutureDates
              />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">To Date</label>
              <DatePicker
                value={draft.closedDateTo}
                onChange={(v) => setDraft({ ...draft, closedDateTo: v })}
                minDate={parseLocalDate(draft.closedDateFrom)}
                disableFutureDates
              />
            </div>
          </div>
        </div>
      </div>
    </FormModal>
  );
}
