"use client";

import { useEffect, useState } from "react";
import { FormModal } from "@/components/lien/modal";
import { BaseSelect } from "@/components/ui/base-select";
import { FilterSection, InfiniteFilterList } from "@/components/lien/filter-section";
import {
  useInfiniteContactOptions,
  useInfiniteCaseManagerOptions,
  useAccidentTypeOptions,
  useCaseStatusOptions,
} from "@/hooks/use-filter-options";

export interface CasesFilterValues {
  lawFirmId: string[];
  accidentTypeId: string[];
  caseManagerId: string[];
  statusId: string[];
}

export const EMPTY_CASES_FILTERS: CasesFilterValues = {
  lawFirmId: [],
  accidentTypeId: [],
  caseManagerId: [],
  statusId: [],
};

interface CasesFilterProps {
  open: boolean;
  onClose: () => void;
  value: CasesFilterValues;
  onApplyFilter: (filters: CasesFilterValues) => void;
  /** From `useBackgroundReady()` — see the same prop on LiensFilter. */
  primaryReady?: boolean;
}

export function CasesFilter({ open, onClose, value, onApplyFilter, primaryReady }: CasesFilterProps) {
  const [draft, setDraft] = useState<CasesFilterValues>(value);

  const listsEnabled = open || !!primaryReady;

  const lawFirms = useInfiniteContactOptions("LawFirm", { enabled: listsEnabled });
  // Scoped to the law firm(s) currently selected in this draft, same as LiensFilter.
  const caseManagers = useInfiniteCaseManagerOptions({
    enabled: listsEnabled,
    lawFirmIds: draft.lawFirmId,
  });
  const accidentTypes = useAccidentTypeOptions();
  const statuses = useCaseStatusOptions();

  // Re-sync the draft with the page's active filters every time the modal opens.
  useEffect(() => {
    if (open) setDraft(value);
  }, [open, value]);

  const handleSubmit = () => {
    onApplyFilter(draft);
    onClose();
  };

  const handleClear = () => setDraft(EMPTY_CASES_FILTERS);

  const handleClose = () => {
    setDraft(value);
    onClose();
  };

  return (
    <FormModal
      open={open}
      onClose={handleClose}
      onSubmit={handleSubmit}
      title="Filter Cases"
      subtitle="Narrow down cases using filters to quickly find relevant results."
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
          {/* Case Manager sits under Law Firm — it's scoped to whichever law
              firm(s) are selected above, so grouping them in one column keeps
              that dependency visually obvious. */}
          <div className="space-y-4">
            <FilterSection
              label="Law Firm"
              source={lawFirms}
              selected={draft.lawFirmId}
              onChange={(v) => setDraft({ ...draft, lawFirmId: v })}
            >
              <InfiniteFilterList
                source={lawFirms}
                searchPlaceholder="Search Law Firm…"
                selected={draft.lawFirmId}
                onChange={(v) => setDraft({ ...draft, lawFirmId: v })}
              />
            </FilterSection>
            <FilterSection
              label="Case Manager"
              source={caseManagers}
              selected={draft.caseManagerId}
              onChange={(v) => setDraft({ ...draft, caseManagerId: v })}
            >
              <InfiniteFilterList
                source={caseManagers}
                searchPlaceholder="Search Case Manager…"
                selected={draft.caseManagerId}
                onChange={(v) => setDraft({ ...draft, caseManagerId: v })}
              />
            </FilterSection>
          </div>
          <div className="space-y-4">
            <FilterSection
              label="Accident Type"
              source={accidentTypes}
              selected={draft.accidentTypeId}
              onChange={(v) => setDraft({ ...draft, accidentTypeId: v })}
            >
              <BaseSelect
                multiple
                inline
                showCheckboxes
                options={accidentTypes.options}
                value={draft.accidentTypeId}
                onChange={(values) => setDraft({ ...draft, accidentTypeId: values })}
                isLoading={accidentTypes.isLoading}
                searchPlaceholder="Search Accident Type…"
                emptyText="No results found"
              />
            </FilterSection>
            <FilterSection
              label="Status"
              source={statuses}
              selected={draft.statusId}
              onChange={(v) => setDraft({ ...draft, statusId: v })}
            >
              <BaseSelect
                multiple
                inline
                showCheckboxes
                options={statuses.options}
                value={draft.statusId}
                onChange={(values) => setDraft({ ...draft, statusId: values })}
                isLoading={statuses.isLoading}
                searchPlaceholder="Search Status…"
                emptyText="No results found"
              />
            </FilterSection>
          </div>
        </div>
      </div>
    </FormModal>
  );
}
