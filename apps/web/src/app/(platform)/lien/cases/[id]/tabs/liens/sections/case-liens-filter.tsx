"use client";

import { useEffect, useState, type ReactNode } from "react";
import { FormModal } from "@/components/lien/modal";
import { DatePicker } from "@/components/ui/date-picker";
import { BaseSelect } from "@/components/ui/base-select";
import {
  useInfiniteContactOptions,
  useLienStatusOptions,
  type InfiniteOptions,
} from "@/hooks/use-filter-options";

export interface CaseLiensFilterValues {
  medicalFacilityIds: string[];
  lienStatusIds: string[];
  purchaseDateFrom: string;
  purchaseDateTo: string;
  initialServiceDateFrom: string;
  initialServiceDateTo: string;
}

function parseLocalDate(value: string): Date | undefined {
  if (!value) return undefined;
  const d = new Date(value + "T00:00:00");
  return isNaN(d.getTime()) ? undefined : d;
}

export const EMPTY_CASE_LIENS_FILTERS: CaseLiensFilterValues = {
  medicalFacilityIds: [],
  lienStatusIds: [],
  purchaseDateFrom: "",
  purchaseDateTo: "",
  initialServiceDateFrom: "",
  initialServiceDateTo: "",
};

export function countActiveCaseLiensFilters(f: CaseLiensFilterValues): number {
  return (
    (f.medicalFacilityIds.length ? 1 : 0) +
    (f.lienStatusIds.length ? 1 : 0) +
    (f.purchaseDateFrom || f.purchaseDateTo ? 1 : 0) +
    (f.initialServiceDateFrom || f.initialServiceDateTo ? 1 : 0)
  );
}

interface CaseLiensFilterProps {
  open: boolean;
  onClose: () => void;
  value: CaseLiensFilterValues;
  onApplyFilter: (filters: CaseLiensFilterValues) => void;
}

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
          {selecting ? "Loading…" : "Select All"}
        </button>
      </div>
      {children}
    </div>
  );
}

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

export function CaseLiensFilter({ open, onClose, value, onApplyFilter }: CaseLiensFilterProps) {
  const [draft, setDraft] = useState<CaseLiensFilterValues>(value);

  const facilities = useInfiniteContactOptions("MedicalFacility", {
    enabled: open,
    mainOnly: true,
  });
  const statuses = useLienStatusOptions({ enabled: open });

  useEffect(() => {
    if (open) setDraft(value);
  }, [open, value]);

  const handleSubmit = () => {
    onApplyFilter(draft);
    onClose();
  };

  const handleClear = () => setDraft(EMPTY_CASE_LIENS_FILTERS);

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
          <p className="text-sm font-medium text-gray-700 mb-2">Initial Service Date</p>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-gray-500 mb-1">From Date</label>
              <DatePicker
                value={draft.initialServiceDateFrom}
                onChange={(v) => setDraft({ ...draft, initialServiceDateFrom: v })}
                maxDate={parseLocalDate(draft.initialServiceDateTo)}
                disableFutureDates
              />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">To Date</label>
              <DatePicker
                value={draft.initialServiceDateTo}
                onChange={(v) => setDraft({ ...draft, initialServiceDateTo: v })}
                minDate={parseLocalDate(draft.initialServiceDateFrom)}
                disableFutureDates
              />
            </div>
          </div>
        </div>
      </div>
    </FormModal>
  );
}
