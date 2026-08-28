"use client";

import { useEffect, useMemo, useState } from "react";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import { CompanyFormModal } from "@/components/selling/forms/company-form-modal";
import { ContactPersonFormModal } from "@/components/selling/forms/contact-person-form-modal";
import { nameSimilarity } from "@/lib/selling/string-similarity";
import {
  useCompanyTypes,
  useCompanies,
  useCompany,
  useContactPerson,
  useContactPersons,
} from "@/hooks/selling/use-selling-companies";

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
  | "MedicalFacility"
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
  /**
   * A name that came in unlinked (e.g. from a bulk-upload row whose provider
   * or company text didn't match any existing record) — shown as a prompt to
   * create that record, prefilled, instead of leaving the field looking
   * empty. Ignored once `value` is set.
   */
  pendingName?: string;
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
  pendingName,
}: SellingEntitySelectProps) {
  const [showCreate, setShowCreate] = useState(false);
  const [createName, setCreateName] = useState<string | undefined>(undefined);

  const showPendingPrompt =
    allowCreate && !isContactPerson && !value && Boolean(pendingName);

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

  // Broadest reasonable term to find candidates for the "close match"
  // suggestions below — the backend search matches on containment, so a
  // single leading word casts a wider net than the full pendingName would;
  // nameSimilarity then re-ranks/filters the results against the full name.
  const suggestSearchTerm = pendingName?.trim().split(/\s+/)[0];
  const suggestQuery = useCompanies(
    { companyTypeId: companyType?.id, search: suggestSearchTerm },
    { enabled: showPendingPrompt && Boolean(companyType?.id) && Boolean(suggestSearchTerm) },
  );
  const SIMILARITY_THRESHOLD = 0.5;
  const suggestions = useMemo(() => {
    if (!showPendingPrompt || !pendingName) return [];
    return (suggestQuery.data?.items ?? [])
      .map((c) => ({ id: c.id, name: c.name, score: nameSimilarity(pendingName, c.name) }))
      .filter((s) => s.score >= SIMILARITY_THRESHOLD)
      .sort((a, b) => b.score - a.score)
      .slice(0, 3);
  }, [showPendingPrompt, pendingName, suggestQuery.data]);
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
            ? {
                label: createLabel,
                onSelect: () => {
                  setCreateName(undefined);
                  setShowCreate(true);
                },
              }
            : undefined
        }
      />

      {showPendingPrompt && (
        <div className="mt-1.5 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2">
          <div className="flex items-start justify-between gap-3">
            <p className="text-xs text-amber-800">
              <span className="font-medium">&ldquo;{pendingName}&rdquo;</span>{" "}
              was imported but doesn&apos;t have a matching record yet.
            </p>
            <button
              type="button"
              className="shrink-0 text-xs font-medium text-amber-900 underline underline-offset-2 hover:text-amber-950"
              onClick={() => {
                setCreateName(pendingName);
                setShowCreate(true);
              }}
            >
              Create it
            </button>
          </div>

          {suggestions.length > 0 && (
            <div className="mt-1.5 flex flex-wrap items-center gap-1.5 border-t border-amber-200 pt-1.5">
              <span className="text-xs text-amber-800">Did you mean:</span>
              {suggestions.map((s) => (
                <button
                  key={s.id}
                  type="button"
                  className="rounded-full border border-amber-300 bg-white px-2 py-0.5 text-xs text-amber-900 hover:bg-amber-100"
                  onClick={() => onChange(s.id, { value: s.id, label: s.name })}
                >
                  {s.name}
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {showCreate && !isContactPerson && companyType && (
        <CompanyFormModal
          open={showCreate}
          onClose={() => setShowCreate(false)}
          title={createLabel}
          companyTypeId={companyType.id}
          lockCompanyType
          initialName={createName}
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
