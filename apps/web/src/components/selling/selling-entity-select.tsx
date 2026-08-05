"use client";

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import { AddContactModal } from "@/components/lien/add-contact-modal";
import type { ContactDetail } from "@/lib/contacts";
import {
  useSellingFundingCompanies,
  useSellingFundingCompanyContacts,
  useSellingFacilities,
  useSellingLawFirms,
  useSellingCaseManagers,
  SELLING_FUNDING_COMPANIES_QUERY_KEY,
  SELLING_FUNDING_COMPANY_CONTACTS_QUERY_KEY,
  SELLING_FACILITIES_QUERY_KEY,
  SELLING_LAW_FIRMS_QUERY_KEY,
  SELLING_CASE_MANAGERS_QUERY_KEY,
} from "@/hooks/use-selling-lookups";

export type SellingEntityType =
  | "FundingCompany"
  | "FundingCompanyContact"
  | "Facility"
  | "LawFirm"
  | "CaseManager";

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

  /** Renders a "+ Add …" row that opens an inline AddContactModal, then refreshes this list. */
  allowCreate?: boolean;
  createLabel?: string;
  /** Contact type/subtype used only when creating a new entity via AddContactModal. */
  createContactType?: string;
  createContactSubtype?: string;
}

function invalidationKey(
  entityType: SellingEntityType,
  fundingCompanyId?: string,
  lawFirmId?: string,
) {
  switch (entityType) {
    case "FundingCompany":
      return SELLING_FUNDING_COMPANIES_QUERY_KEY;
    case "FundingCompanyContact":
      return SELLING_FUNDING_COMPANY_CONTACTS_QUERY_KEY(fundingCompanyId ?? "");
    case "Facility":
      return SELLING_FACILITIES_QUERY_KEY;
    case "LawFirm":
      return SELLING_LAW_FIRMS_QUERY_KEY;
    case "CaseManager":
      return SELLING_CASE_MANAGERS_QUERY_KEY(lawFirmId ?? "");
  }
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
  createContactType,
  createContactSubtype,
}: SellingEntitySelectProps) {
  const queryClient = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);

  const parentId = entityType === "CaseManager" ? lawFirmId : fundingCompanyId;
  const parentMissing = Boolean(requireParent) && !parentId;

  // All five lookups are queried unconditionally (rather than dispatched by
  // entityType) so this component never changes which hooks it calls across
  // renders — each query is individually gated by `enabled` (see
  // use-selling-lookups.ts) so only the one matching entityType actually hits
  // the network.
  const fundingCompanies = useSellingFundingCompanies({
    enabled: entityType === "FundingCompany",
  });
  const fundingCompanyContacts = useSellingFundingCompanyContacts(fundingCompanyId, {
    enabled: entityType === "FundingCompanyContact",
  });
  const facilities = useSellingFacilities({ enabled: entityType === "Facility" });
  const lawFirms = useSellingLawFirms({ enabled: entityType === "LawFirm" });
  const caseManagers = useSellingCaseManagers(lawFirmId, {
    enabled: entityType === "CaseManager",
  });

  const { options, isLoading } = {
    FundingCompany: fundingCompanies,
    FundingCompanyContact: fundingCompanyContacts,
    Facility: facilities,
    LawFirm: lawFirms,
    CaseManager: caseManagers,
  }[entityType];

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

      {showCreate && (
        <AddContactModal
          open={showCreate}
          onClose={() => setShowCreate(false)}
          title={createLabel}
          contactType={createContactType}
          contactSubtype={createContactSubtype}
          lawFirmId={entityType === "CaseManager" ? lawFirmId : undefined}
          facilityId={entityType === "FundingCompanyContact" ? fundingCompanyId : undefined}
          onSaved={async (created: ContactDetail) => {
            const queryKey = invalidationKey(entityType, fundingCompanyId, lawFirmId);
            await queryClient.invalidateQueries({ queryKey });

            const refreshed = queryClient.getQueryData<{ id: string }[]>(queryKey);
            const stillListed = refreshed?.some((item) => item.id === created.id);

            if (stillListed ?? true) {
              onChange(created.id, { value: created.id, label: created.displayName });
            } else {
              toast.error("Couldn't select the new entry", {
                description:
                  "It was created but didn't come back in the refreshed list. Please select it manually.",
              });
            }
            setShowCreate(false);
          }}
        />
      )}
    </>
  );
}
