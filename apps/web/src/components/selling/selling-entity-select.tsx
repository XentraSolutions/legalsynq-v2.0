"use client";

import { useMemo, useState } from "react";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import { CompanyFormModal } from "@/components/selling/forms/company-form-modal";
import { ContactPersonFormModal } from "@/components/selling/forms/contact-person-form-modal";
import { useCompanyTypes, useCompanies, useCompany, useContactPersons } from "@/hooks/use-selling-companies";

export type SellingEntityType =
  | "FundingCompany"
  | "FundingCompanyContact"
  | "Facility"
  | "LawFirm"
  | "CaseManager";

// Company-type / contact-person-type *names*, matched by name against
// GET /lookups/company-types and /lookups/contact-person-types (ids are
// backend-assigned, so name is the only stable thing to key off of here).
// Update these if the backend seeds different display names.
const COMPANY_TYPE_NAME: Partial<Record<SellingEntityType, string>> = {
  FundingCompany: "Funding Company",
  Facility: "Facility",
  LawFirm: "Law Firm",
};

const CONTACT_PERSON_TYPE_NAME: Partial<Record<SellingEntityType, string>> = {
  FundingCompanyContact: "Contact Person",
  CaseManager: "Case Manager",
};

// entityType -> the SellingEntityType whose company-type the contact
// person's parent company belongs to.
const PARENT_COMPANY_ENTITY_TYPE: Partial<Record<SellingEntityType, SellingEntityType>> = {
  FundingCompanyContact: "FundingCompany",
  CaseManager: "LawFirm",
};

interface SellingEntitySelectProps {
  entityType: SellingEntityType;
  /** Required for FundingCompanyContact; scopes the contacts list to a funding company. */
  fundingCompanyId?: string;
  /** Required for CaseManager; scopes the case managers list to a law firm. */
  lawFirmId?: string;
  requireParent?: boolean;
  parentHint?: string;
  disabled?: boolean;

  value?: string | null;
  onChange: (value: string, option: BaseSelectOption) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  error?: boolean;
  className?: string;

  /** Renders a "+ Add …" row that opens an inline create modal, then refreshes this list. */
  allowCreate?: boolean;
  createLabel?: string;
}

export function SellingEntitySelect({
  entityType,
  fundingCompanyId,
  lawFirmId,
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
}: SellingEntitySelectProps) {
  const [showCreate, setShowCreate] = useState(false);

  const isContactPerson = entityType === "FundingCompanyContact" || entityType === "CaseManager";
  const companyId = entityType === "CaseManager" ? lawFirmId : fundingCompanyId;
  const parentMissing = Boolean(requireParent) && isContactPerson && !companyId;

  const companyTypesQuery = useCompanyTypes();
  const companyTypeName = isContactPerson
    ? COMPANY_TYPE_NAME[PARENT_COMPANY_ENTITY_TYPE[entityType]!]
    : COMPANY_TYPE_NAME[entityType];
  const companyType = companyTypesQuery.data?.find((t) => t.name === companyTypeName);

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
  const contactPersonTypeName = isContactPerson ? CONTACT_PERSON_TYPE_NAME[entityType] : undefined;
  const contactPersonOptions = useMemo(() => {
    if (!isContactPerson) return [];
    const items = contactPersonsQuery.data ?? [];
    const filtered = contactPersonTypeName
      ? items.filter((c) => c.contactPersonTypeName === contactPersonTypeName)
      : items;
    return filtered.map((c) => ({ value: c.id, label: c.displayName }));
  }, [isContactPerson, contactPersonsQuery.data, contactPersonTypeName]);

  const options = isContactPerson ? contactPersonOptions : companiesQuery.options;
  const isLoading = isContactPerson ? contactPersonsQuery.isLoading : companiesQuery.isLoading;

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
          onSaved={(created) => {
            onChange(created.id, { value: created.id, label: created.displayName });
            setShowCreate(false);
          }}
        />
      )}
    </>
  );
}
