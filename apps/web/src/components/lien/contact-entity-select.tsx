"use client";

import { useEffect, useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import {
  useInfiniteContacts,
  useContact,
  useMedicalFacilityByFacilityId,
  INFINITE_CONTACTS_QUERY_KEY,
} from "@/hooks/use-contacts";
import { AddContactModal } from "@/components/lien/add-contact-modal";
import type { ContactDetail } from "@/lib/contacts";

export type ContactEntityType =
  | "LawFirm"
  | "MedicalFacility"
  | "Provider"
  | "FundingCompany"
  | "Lead";

interface ContactEntitySelectProps {
  contactType: ContactEntityType;
  /** Fixed sub-contact role, e.g. the resolved case-manager code or "FacilityContactPerson". */
  contactSubtype?: string;
  /** Scopes LawFirm sub-contacts (case manager) to a specific law firm. */
  lawFirmId?: string;
  /** Scopes MedicalFacility sub-contacts (facility contact person) to a specific facility. */
  facilityId?: string;
  /** When true, the select stays disabled and shows `parentHint` until its scoping parent id is set. */
  requireParent?: boolean;
  parentHint?: string;
  /** Forces the select non-interactive regardless of parent scoping. */
  disabled?: boolean;

  value?: string | null;
  onChange: (value: string, option: BaseSelectOption | null) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  error?: boolean;
  className?: string;

  /** Renders a "+ Add …" row that opens an inline AddContactModal scoped to this entity. */
  allowCreate?: boolean;
  createLabel?: string;
}

export function ContactEntitySelect({
  contactType,
  contactSubtype,
  lawFirmId,
  facilityId,
  requireParent,
  parentHint = "Select a parent option first",
  disabled,
  value,
  onChange,
  placeholder = "Select...",
  searchPlaceholder = "Search...",
  error,
  className,
  allowCreate,
  createLabel = "Add New",
}: ContactEntitySelectProps) {
  const queryClient = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [selectedOption, setSelectedOption] = useState<BaseSelectOption | null>(null);

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(timeout);
  }, [search]);

  const parentId = lawFirmId ?? facilityId;
  const parentMissing = Boolean(requireParent) && !parentId;

  const query = {
    ContactType: contactType,
    ContactSubtype: contactSubtype,
    LawFirmId: lawFirmId,
    FacilityId: facilityId,
  };

  const {
    data,
    isLoading,
    isFetching,
    isFetchingNextPage,
    hasNextPage,
    fetchNextPage,
  } = useInfiniteContacts(
    { ...query, search: debouncedSearch || undefined },
    { enabled: !parentMissing },
  );

  // A debounced-search refetch in flight — distinct from the very first
  // load (isLoading) and from paginating (isFetchingNextPage), both of
  // which have their own skeleton treatment already.
  const isSearching = isFetching && !isLoading && !isFetchingNextPage;

  const fetched: BaseSelectOption[] = useMemo(
    () =>
      (data?.pages ?? []).flatMap((page) =>
        page.items.map((c) => ({ value: c.id, label: c.displayName })),
      ),
    [data],
  );

  // A value can arrive pre-set from saved data (e.g. an existing facility's
  // contact id) without ever having been chosen through this dropdown, so
  // there's no label for it yet — resolve it directly by id.
  const needsResolve =
    Boolean(value) && selectedOption?.value !== value && !fetched.some((o) => o.value === value);
  const { data: resolvedContact, isError: resolveByIdFailed } = useContact(
    needsResolve ? value : undefined,
  );

  // Medical facility liens predate the unified Contacts system and store a
  // facility's own `facilityId`, not its contact id — if the direct by-id
  // lookup above comes up empty, fall back to resolving it that way. Only
  // applies to the *main* MedicalFacility contact (no contactSubtype) — see
  // the doc on ContactResponseDto.facilityId in contacts.types.ts for why
  // that field means something else entirely on a FacilityContactPerson
  // sub-contact (a Contact id, not a legacy Facility id).
  const isMedicalFacilitySelect = contactType === "MedicalFacility" && !contactSubtype;
  const needsFacilityFallback = needsResolve && isMedicalFacilitySelect && resolveByIdFailed;
  const { data: facilityFallbackContact } = useMedicalFacilityByFacilityId(
    needsFacilityFallback ? value : undefined,
  );

  useEffect(() => {
    if (resolvedContact && resolvedContact.id === value) {
      setSelectedOption({ value: resolvedContact.id, label: resolvedContact.displayName });
    }
  }, [resolvedContact, value]);

  useEffect(() => {
    if (facilityFallbackContact && value && needsFacilityFallback) {
      setSelectedOption({ value, label: facilityFallbackContact.displayName });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [facilityFallbackContact, value]);

  useEffect(() => {
    if (!value) {
      setSelectedOption(null);
    }
  }, [value]);

  const options: BaseSelectOption[] = useMemo(() => {
    // The selected item may not be in the pages loaded so far — keep it
    // around so the trigger can still show its label instead of reverting
    // to the placeholder.
    if (value && selectedOption?.value === value && !fetched.some((o) => o.value === value)) {
      return [selectedOption, ...fetched];
    }
    return fetched;
  }, [fetched, value, selectedOption]);

  const handleChange = (nextValue: string, option: BaseSelectOption | null) => {
    setSelectedOption(option);
    onChange(nextValue, option);
  };

  return (
    <>
      <BaseSelect
        value={value}
        onChange={handleChange}
        options={options}
        loadingMode="infinite"
        isLoading={isLoading}
        isSearching={isSearching}
        isFetchingMore={isFetchingNextPage}
        hasNextPage={hasNextPage}
        onLoadMore={fetchNextPage}
        disabled={disabled || parentMissing}
        placeholder={parentMissing ? parentHint : placeholder}
        searchPlaceholder={searchPlaceholder}
        search={search}
        onSearchChange={setSearch}
        filterLocally={false}
        error={error}
        className={className}
        createAction={
          allowCreate && !parentMissing
            ? { label: createLabel, onSelect: () => setShowCreate(true) }
            : undefined
        }
      />

      {showCreate && (
        <AddContactModal
          open={showCreate}
          onClose={() => setShowCreate(false)}
          title={createLabel}
          contactType={contactType}
          contactSubtype={contactSubtype}
          lawFirmId={lawFirmId}
          facilityId={facilityId}
          onSaved={(created: ContactDetail) => {
            queryClient.invalidateQueries({ queryKey: INFINITE_CONTACTS_QUERY_KEY(query) });
            handleChange(created.id, { value: created.id, label: created.displayName });
            setShowCreate(false);
          }}
        />
      )}
    </>
  );
}
