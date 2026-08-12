"use client";

import { useMemo, useState } from "react";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import { CompanyFormModal } from "@/components/selling/forms/company-form-modal";
import { ContactPersonFormModal } from "@/components/selling/forms/contact-person-form-modal";
import {
  useCompanyTypes,
  useCompanies,
  useCompany,
  useContactPersons,
} from "@/hooks/use-selling-companies";

/** A company type (`GET /lookups/company-types`), matched by its `code`. */
export type SellingEntityType = "FundingCompany" | "Facility" | "LawFirm";

interface SellingEntitySelectProps {
  /** The company type to select from. When `isContactPerson` is set, this is the type of the *parent* company whose contacts are listed. */
  entityType: SellingEntityType;
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

  const companiesQuery = useCompanies(
    { companyTypeId: companyType?.id },
    { enabled: !isContactPerson && Boolean(companyType?.id) },
  );

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

  const options = isContactPerson
    ? contactPersonOptions
    : companiesQuery.options;
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
        disabled={disabled || parentMissing}
        placeholder={parentMissing ? parentHint : placeholder}
        searchPlaceholder={searchPlaceholder}
        error={error}
        className={className}
        clearable
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
