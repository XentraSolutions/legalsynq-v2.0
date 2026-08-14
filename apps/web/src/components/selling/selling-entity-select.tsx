"use client";

import { useEffect, useMemo, useState } from "react";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import { CompanyFormModal } from "@/components/selling/forms/company-form-modal";
import { ContactPersonFormModal } from "@/components/selling/forms/contact-person-form-modal";
import {
  useCompanyTypes,
  useCompanies,
  useCompany,
  useContactPerson,
  useContactPersons,
} from "@/hooks/use-selling-companies";

/**
 * Prepends `resolved` (a single record fetched by id because it wasn't in
 * `base`) ahead of `base`, deduping by `value` — if `base` has since caught
 * up with `resolved` (e.g. a later page load, or a refetch), the merge
 * always keeps one copy rather than showing the option twice.
 */
function mergeSelected(
  base: BaseSelectOption[],
  resolved: BaseSelectOption | undefined,
): BaseSelectOption[] {
  if (!resolved) return base;
  return [resolved, ...base.filter((o) => o.value !== resolved.value)];
}

/** A company type (`GET /lookups/company-types`), matched by its `code`. */
export type SellingEntityType =
  | "FundingCompany"
  | "Facility"
  | "LawFirm"
  | "MedicalProvider";

interface SellingEntitySelectProps {
  /**
   * The company type to select from. When `isContactPerson` is set, this is
   * the type of the *parent* company whose contacts are listed. Omit to list
   * companies across all types (e.g. a generic "pick any company" field) —
   * only meaningful when `isContactPerson` is unset, since a contact list
   * always needs a specific parent company.
   */
  entityType?: SellingEntityType;
  /** Required when `isContactPerson` is set — scopes the contacts list to this company. */
  companyId?: string;
  requireParent?: boolean;
  parentHint?: string;
  disabled?: boolean;

  value?: string | null;
  onChange: (value: string, option: BaseSelectOption | null) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  error?: boolean;
  className?: string;

  /** Renders a "+ Add …" row that opens an inline create modal, then refreshes this list. */
  allowCreate?: boolean;
  createLabel?: string;
  /** Lists `companyId`'s contact persons instead of companies of `entityType`. */
  isContactPerson?: boolean;
  /** Client-side filter on the contact persons list by role code.
   * TODO: ask for API support filtering a company's contact persons by
   * contact-person-type, to avoid this client-side filtering. */
  contactType?: "CaseManager" | "Attorney";
}

export function SellingEntitySelect({
  entityType,
  companyId,
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
  isContactPerson,
  contactType,
}: SellingEntitySelectProps) {
  const [showCreate, setShowCreate] = useState(false);

  const parentMissing = Boolean(requireParent) && isContactPerson && !companyId;

  const companyTypesQuery = useCompanyTypes();
  const companyType = companyTypesQuery.data?.find(
    (t) => t.code === entityType,
  );

  // Server-side search, not just client-side filtering of one loaded page —
  // an unscoped (no entityType) company list, or any type with more
  // companies than a page holds, otherwise can't find a match past that
  // page (see create-contact-person.spec.ts).
  const [companySearch, setCompanySearch] = useState("");
  const [debouncedCompanySearch, setDebouncedCompanySearch] = useState("");
  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedCompanySearch(companySearch), 300);
    return () => clearTimeout(timeout);
  }, [companySearch]);

  const companiesQuery = useCompanies(
    { companyTypeId: companyType?.id, search: debouncedCompanySearch || undefined },
    { enabled: !isContactPerson && (!entityType || Boolean(companyType?.id)) },
  );
  // A debounced-search refetch in flight, distinct from the very first load —
  // BaseSelect shows a different skeleton for each.
  const isSearchingCompanies =
    companiesQuery.isFetching && !companiesQuery.isLoading;

  const contactPersonsQuery = useContactPersons(companyId, true, {
    enabled: isContactPerson && !parentMissing,
  });
  const parentCompanyQuery = useCompany(companyId, {
    enabled: isContactPerson && showCreate && Boolean(companyId),
  });
  const contactPersonOptions = useMemo(() => {
    if (!isContactPerson) return [];
    const items = contactPersonsQuery.data ?? [];
    const filtered = contactType
      ? items.filter((c) => c.contactPersonTypeCode === contactType)
      : items;
    return filtered.map((c) => ({ value: c.id, label: c.displayName }));
  }, [isContactPerson, contactPersonsQuery.data, contactType]);

  // `value` may point at a record the list above doesn't currently contain —
  // e.g. a contact filtered out by the isActive/contactType scoping, or a
  // company sitting past whatever page a paginated list has loaded. Fetch it
  // by id and merge it in so the trigger can still show its label instead of
  // falling back to the placeholder.
  const selectedCompanyMissing =
    !isContactPerson && Boolean(value) && !companiesQuery.options.some((o) => o.value === value);
  const selectedCompanyQuery = useCompany(value, {
    enabled: selectedCompanyMissing,
  });

  const selectedContactMissing =
    isContactPerson &&
    Boolean(value) &&
    Boolean(companyId) &&
    !contactPersonOptions.some((o) => o.value === value);
  const selectedContactQuery = useContactPerson(companyId, value, {
    enabled: selectedContactMissing,
  });

  const options = useMemo(() => {
    if (isContactPerson) {
      const resolved =
        selectedContactMissing && selectedContactQuery.data
          ? {
              value: selectedContactQuery.data.id,
              label: selectedContactQuery.data.displayName,
            }
          : undefined;
      return mergeSelected(contactPersonOptions, resolved);
    }
    const resolved =
      selectedCompanyMissing && selectedCompanyQuery.data
        ? { value: selectedCompanyQuery.data.id, label: selectedCompanyQuery.data.name }
        : undefined;
    return mergeSelected(companiesQuery.options, resolved);
  }, [
    isContactPerson,
    contactPersonOptions,
    selectedContactQuery.data,
    selectedContactMissing,
    companiesQuery.options,
    selectedCompanyQuery.data,
    selectedCompanyMissing,
  ]);
  const isLoading = isContactPerson
    ? contactPersonsQuery.isLoading
    : companiesQuery.isLoading;

  return (
    <>
      <BaseSelect
        value={value}
        onChange={onChange}
        options={options}
        isLoading={isLoading}
        isSearching={!isContactPerson ? isSearchingCompanies : undefined}
        disabled={disabled || parentMissing}
        placeholder={parentMissing ? parentHint : placeholder}
        searchPlaceholder={searchPlaceholder}
        error={error}
        className={className}
        clearable
        search={!isContactPerson ? companySearch : undefined}
        onSearchChange={!isContactPerson ? setCompanySearch : undefined}
        filterLocally={isContactPerson}
        createAction={
          allowCreate && !parentMissing
            ? { label: createLabel, onSelect: () => setShowCreate(true) }
            : undefined
        }
      />

      {showCreate && !isContactPerson && companyType && (
        <CompanyFormModal
          open={showCreate}
          onClose={() => setShowCreate(false)}
          title={createLabel}
          companyTypeId={companyType.id}
          lockCompanyType
          onSaved={(created) => {
            onChange(created.id, { value: created.id, label: created.name });
            setShowCreate(false);
          }}
        />
      )}

      {showCreate && isContactPerson && companyId && companyType && (
        <ContactPersonFormModal
          open={showCreate}
          onClose={() => setShowCreate(false)}
          title={createLabel}
          companyId={companyId}
          companyName={parentCompanyQuery.data?.name ?? ""}
          companyTypeId={companyType.id}
          lockContactType={contactType}
          onSaved={(created) => {
            onChange(created.id, {
              value: created.id,
              label: created.displayName,
            });
            setShowCreate(false);
          }}
        />
      )}
    </>
  );
}
