"use client";

import { useEffect, useState } from "react";
import { FormModal } from "@/components/selling/modal";
import { DatePicker } from "@/components/ui/date-picker";

import {
  FilterSection,
  InfiniteFilterList,
} from "@/components/lien/filter-section";
import { useInfiniteCompanyOptions } from "@/hooks/use-selling-companies";
import { Button } from "@/components/ui/button";

export interface LiensFilterValues {
  tab: string;
  search: string | undefined;
  fundingCompanyIds: string[];
  lienStatusIds: string[];
  // caseManagerIds: string[];
  // facilityIds: string[];
  initialServiceDateFrom: string;
  initialServiceDateTo: string;
  sortBy: string[];
  initialServiceDate: string;
  sortDirection: string;
  page: number;
  pageSize: number;
}

function parseLocalDate(value: string): Date | undefined {
  if (!value) return undefined;
  const d = new Date(value + "T00:00:00");
  return isNaN(d.getTime()) ? undefined : d;
}

export const EMPTY_LIENS_FILTERS: LiensFilterValues = {
  tab: "Pending",
  search: "",
  fundingCompanyIds: [],
  lienStatusIds: [],
  // caseManagerIds: [],
  // facilityIds: [],
  initialServiceDateFrom: "",
  initialServiceDateTo: "",
  sortBy: [],
  initialServiceDate: "",
  sortDirection: "asc",
  page: 1,
  pageSize: 20,
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

export function LiensFilter({
  open,
  onClose,
  value,
  onApplyFilter,
  primaryReady,
}: LiensFilterProps) {
  const [draft, setDraft] = useState<LiensFilterValues>(value);

  // Enabled once the modal is open OR the page's primary content is ready —
  // whichever happens first — so these lists are usually already warm by
  // the time someone opens the modal instead of starting cold right then.
  const listsEnabled = open || !!primaryReady;

  const fundingCompany = useInfiniteCompanyOptions("FundingCompany", {
    enabled: listsEnabled,
  });

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
        <Button
          type="button"
          variant="ghost"
          leftIcon={<i className="ri-refresh-line text-sm" />}
          className="text-xs px-3 py-1.5 text-primary border border-primary/30 hover:bg-primary/5"
          onClick={handleClear}
        >
          Clear Filter
        </Button>
      }
    >
      <div className="space-y-5">
        <div>
          <FilterSection
            label="Funding Company"
            source={fundingCompany}
            selected={draft.fundingCompanyIds}
            onChange={(v) => setDraft({ ...draft, fundingCompanyIds: v })}
          >
            <InfiniteFilterList
              source={fundingCompany}
              searchPlaceholder="Search Funding Company"
              selected={draft.fundingCompanyIds}
              onChange={(v) => setDraft({ ...draft, fundingCompanyIds: v })}
            />
          </FilterSection>
        </div>
        <div>
          <p className="text-sm font-medium text-gray-700 mb-2">
            Initial Service Date
          </p>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-gray-500 mb-1">
                From Date
              </label>
              <DatePicker
                value={draft.initialServiceDateFrom}
                onChange={(v) =>
                  setDraft({ ...draft, initialServiceDateFrom: v })
                }
                // Can't be after the To date already picked (also implies
                // "not in the future", since To is itself capped at today).
                maxDate={parseLocalDate(draft.initialServiceDateTo)}
                disableFutureDates
              />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">
                To Date
              </label>
              <DatePicker
                value={draft.initialServiceDateTo}
                onChange={(v) =>
                  setDraft({ ...draft, initialServiceDateTo: v })
                }
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
