"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import { useCasesSearch } from "@/hooks/use-cases-search";

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

// Case picker for the lien wizard's step 1. Search hits the real,
// already-used case-search endpoint (casesService.getCases /
// casesApi.listBySearch) — only case *creation* is stubbed for now (see
// @/components/selling/case-wizard). "+ Add New Case" leaves the lien
// wizard entirely and opens the standalone case wizard.
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
  const { items, isFetching } = useCasesSearch(search, { enabled: true });

  const options: CaseOption[] = useMemo(
    () =>
      items.map((c) => ({
        value: c.id,
        label: `${c.caseNumber} ${c.clientName}`,
        caseNumber: c.caseNumber,
        clientName: c.clientName,
      })),
    [items],
  );

  return (
    <BaseSelect<CaseOption>
      value={value}
      onChange={onChange}
      options={options}
      isLoading={isFetching}
      isSearching={isFetching}
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
