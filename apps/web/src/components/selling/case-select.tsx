"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import { useInfiniteCasesSearch, useCase } from "@/hooks/selling/use-cases-search";

interface CaseOption extends BaseSelectOption {
  caseNumber: string;
  clientName: string;
}

export interface CaseSelectProps {
  value?: string;
  onChange: (value: string, option: CaseOption | null) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  error?: boolean;
  className?: string;
}

function toOption(c: { id: string; caseNumber: string; clientName: string }): CaseOption {
  return {
    value: c.id,
    label: `${c.caseNumber} ${c.clientName}`,
    caseNumber: c.caseNumber,
    clientName: c.clientName,
  };
}

/**
 * Prepends `resolved` (a single record fetched by id because it wasn't in
 * `base`) ahead of `base`, deduping by `value` — same helper as
 * selling-entity-select.tsx's `mergeSelected`.
 */
function mergeSelected(base: CaseOption[], resolved: CaseOption | undefined): CaseOption[] {
  if (!resolved) return base;
  return [resolved, ...base.filter((o) => o.value !== resolved.value)];
}

// Case picker for the lien wizard's step 1.
//
// TODO(backend): there is no lien-selling-specific case list/search API yet.
// This borrows the general (non-selling) Cases module's search endpoint
// (casesService.getCases / casesApi.listBySearch, via
// @/hooks/selling/use-cases-search) as a stand-in — an assumption, not a confirmed
// selling data contract. Swap to a real Selling case-search endpoint (and
// @/lib/selling's own types/api/service, per this repo's Selling-has-its-own-
// data-layer rule) once one exists; case *creation* is separately stubbed
// too (see @/components/selling/case-wizard). Scroll-paginated +
// debounced-search + resolve-missing-selection-by-id, same shape as
// ContactEntitySelect / SellingEntitySelect.
export function CaseSelect({
  value,
  onChange,
  placeholder = "Select case",
  searchPlaceholder = "Search cases...",
  error,
  className,
}: CaseSelectProps) {
  const router = useRouter();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(timeout);
  }, [search]);

  const casesQuery = useInfiniteCasesSearch(debouncedSearch, { enabled: true });
  // A debounced-search refetch in flight, distinct from the very first load
  // and from paginating — each has its own skeleton treatment in BaseSelect.
  const isSearching =
    casesQuery.isFetching && !casesQuery.isLoading && !casesQuery.isFetchingNextPage;

  const fetched: CaseOption[] = useMemo(
    () =>
      (casesQuery.data?.pages ?? []).flatMap((page) => page.items.map(toOption)),
    [casesQuery.data],
  );

  // `value` may point at a case the list above doesn't currently contain —
  // e.g. one just created in the case wizard, or one sitting past whatever
  // page has loaded so far. Fetch it by id and merge it in so the trigger
  // can still show its label instead of falling back to the placeholder.
  const selectedMissing = Boolean(value) && !fetched.some((o) => o.value === value);
  const selectedCaseQuery = useCase(value, { enabled: selectedMissing });

  const options = useMemo(() => {
    const resolved =
      selectedMissing && selectedCaseQuery.data && selectedCaseQuery.data.id === value
        ? toOption(selectedCaseQuery.data)
        : undefined;
    return mergeSelected(fetched, resolved);
  }, [fetched, selectedMissing, selectedCaseQuery.data, value]);

  return (
    <BaseSelect<CaseOption>
      value={value}
      onChange={onChange}
      options={options}
      loadingMode="infinite"
      isLoading={casesQuery.isLoading}
      isSearching={isSearching}
      isFetchingMore={casesQuery.isFetchingNextPage}
      hasNextPage={casesQuery.hasNextPage}
      onLoadMore={casesQuery.fetchNextPage}
      search={search}
      onSearchChange={setSearch}
      filterLocally={false}
      placeholder={placeholder}
      searchPlaceholder={searchPlaceholder}
      error={error}
      className={className}
      renderOption={(option, { selected, active }) => (
        <div className={`flex flex-col ${selected || active ? "" : ""}`}>
          <span className="text-sm font-medium text-gray-900">
            {option.caseNumber}
          </span>
          <span className="text-xs text-gray-500">{option.clientName}</span>
        </div>
      )}
      createAction={{
        label: "Add New Case",
        onSelect: () => router.push("/selling/portfolio/cases/add"),
      }}
    />
  );
}
