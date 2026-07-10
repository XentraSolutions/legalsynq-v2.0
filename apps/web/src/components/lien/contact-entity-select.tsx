"use client";

import { useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import { useContacts, CONTACTS_QUERY_KEY } from "@/hooks/use-contacts";
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

  value?: string | null;
  onChange: (value: string, option: BaseSelectOption) => void;
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

  const parentId = lawFirmId ?? facilityId;
  const parentMissing = Boolean(requireParent) && !parentId;

  const query = {
    ContactType: contactType,
    ContactSubtype: contactSubtype,
    LawFirmId: lawFirmId,
    FacilityId: facilityId,
  };

  const { data, isLoading } = useContacts(query, { enabled: !parentMissing });

  const options: BaseSelectOption[] = useMemo(
    () => (data?.items ?? []).map((c) => ({ value: c.id, label: c.displayName })),
    [data],
  );

  return (
    <>
      <BaseSelect
        value={value}
        onChange={onChange}
        options={options}
        isLoading={isLoading}
        disabled={parentMissing}
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
          contactType={contactType}
          contactSubtype={contactSubtype}
          lawFirmId={lawFirmId}
          facilityId={facilityId}
          onSaved={(created: ContactDetail) => {
            queryClient.invalidateQueries({ queryKey: CONTACTS_QUERY_KEY(query) });
            onChange(created.id, { value: created.id, label: created.displayName });
            setShowCreate(false);
          }}
        />
      )}
    </>
  );
}
