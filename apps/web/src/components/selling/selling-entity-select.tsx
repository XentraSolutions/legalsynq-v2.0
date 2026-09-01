"use client";

import { useEffect, useMemo, useState } from "react";
import { Plus, TriangleAlert, Users } from "lucide-react";
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
  // The top suggestion's text is the same as what was imported — nothing
  // ambiguous about the name itself, just whether it's the same real-world
  // company (see the "Create it" fallback for when it isn't). Drives the
  // copy below so it doesn't claim "no matching record" when there plainly
  // is one, textually.
  const exactSuggestion = suggestions.find(
    (s) => s.name.trim().toLowerCase() === pendingName?.trim().toLowerCase(),
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

  // "Add Case Manager" -> "case manager", used to build the empty-state copy
  // below without hard-coding it to one entity type.
  const contactNoun = createLabel.replace(/^Add\s+/i, "").toLowerCase();
  const contactEmptyState =
    isContactPerson && allowCreate && !parentMissing ? (
      <div className="flex flex-col">
        <div className="flex flex-col items-center gap-3 px-4 py-6 text-center">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-gray-100">
            <Users className="h-5 w-5 text-gray-900" />
          </div>
          <div>
            <p className="text-sm font-semibold text-gray-900">
              No Available {createLabel.replace(/^Add\s+/i, "")}
            </p>
            <p className="mt-1 text-sm text-gray-500">
              No {contactNoun}s are available at the moment. Add a{" "}
              {contactNoun} using the button below.
            </p>
          </div>
        </div>
        <button
          type="button"
          className="flex w-full items-center gap-1.5 text-left px-3 py-2 text-sm font-semibold text-primary border-t border-gray-100 hover:bg-gray-50"
          onClick={() => {
            setCreateName(undefined);
            setShowCreate(true);
          }}
        >
          <Plus className="h-3.5 w-3.5" />
          {createLabel}
        </button>
      </div>
    ) : undefined;

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
        emptyState={contactEmptyState}
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
        <div className="mt-1.5 flex flex-col items-start gap-3 self-stretch rounded-[10px] bg-[rgba(254,252,232,0.5)] px-4 py-3">
          <div>
            <div className="flex items-start gap-2">
              <TriangleAlert className="h-4 w-4 shrink-0 text-amber-600 mt-0.5" />
              <p className="text-sm text-gray-700">
                <span className="font-medium">&ldquo;{pendingName}&rdquo;</span>{" "}
                {exactSuggestion
                  ? "matches an existing record — confirm below to link it."
                  : suggestions.length > 0
                    ? "was imported, but we couldn't confidently match it to an existing record. Pick the closest match below, or create a new one."
                    : "was imported, but we couldn't find a matching record. Create a new one below."}
              </p>
            </div>
            <button
              type="button"
              className="mt-1 flex items-center gap-1 pl-6 text-sm font-medium text-[#EE7132] hover:text-[#D9672E]"
              onClick={() => {
                setCreateName(pendingName);
                setShowCreate(true);
              }}
            >
              <Plus className="h-4 w-4" />
              Create it
            </button>
          </div>

          {suggestions.length > 0 && (
            <div className="flex w-full flex-col items-start gap-3 border-t border-[#E5E5E5] pt-3">
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-sm text-gray-500">Suggested Match:</span>
                {suggestions.map((s) => (
                  <button
                    key={s.id}
                    type="button"
                    className="rounded-full border border-[#E5E5E5] bg-white px-3 py-1 text-sm text-gray-900 hover:bg-gray-50"
                    onClick={() => onChange(s.id, { value: s.id, label: s.name })}
                  >
                    {s.name}
                  </button>
                ))}
              </div>
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
