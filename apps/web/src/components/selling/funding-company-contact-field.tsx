"use client";

import { useEffect, useState } from "react";
import { Mail, Phone, TriangleAlert } from "lucide-react";
import { Button } from "@/components/selling/button";
import { ContactPersonFormModal } from "@/components/selling/forms/contact-person-form-modal";
import { useCompanyTypes, useContactPersons } from "@/hooks/selling/use-selling-companies";
import type { ContactPerson } from "@/lib/selling/companies.types";

interface FundingCompanyContactFieldProps {
  /** The selected funding company; the field renders nothing until this is set. */
  companyId: string;
  /** Display label for the company — passed through to the create modal's subtitle. */
  companyName?: string;
  /** Currently selected contact id, kept in sync by `onChange` once contacts load. */
  value?: string;
  onChange: (contactId: string, contact: ContactPerson | null) => void;
  label?: string;
  required?: boolean;
  className?: string;
}

/**
 * A funding company either has a contact person already — in which case
 * there's nothing to choose, so the field just displays it and preselects
 * it — or it has none, in which case the field prompts to add the first one
 * inline. Once added, the field re-renders as the display state on its own.
 *
 * Used by both the lien-associations wizard step (provider-funding-fields)
 * and the sell-lien buyer-selection step, so the UX matches in both places.
 */
export function FundingCompanyContactField({
  companyId,
  companyName = "",
  value,
  onChange,
  label = "Contact Person",
  required,
  className,
}: FundingCompanyContactFieldProps) {
  const [showAdd, setShowAdd] = useState(false);
  const companyTypesQuery = useCompanyTypes();
  const fundingCompanyType = companyTypesQuery.data?.find(
    (t) => t.code === "FundingCompany",
  );

  const contactPersonsQuery = useContactPersons(companyId || null, true, {
    enabled: Boolean(companyId),
  });
  const contacts = contactPersonsQuery.data ?? [];
  const firstContact = contacts[0] ?? null;

  // Preselect the first (and, per the "hide add when there's one" rule,
  // only) contact whenever the loaded list disagrees with the current value.
  useEffect(() => {
    if (contactPersonsQuery.isLoading) return;
    if (firstContact) {
      if (value !== firstContact.id) onChange(firstContact.id, firstContact);
    } else if (value) {
      onChange("", null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [contactPersonsQuery.isLoading, firstContact?.id]);

  if (!companyId) return null;

  return (
    <div className={className}>
      <label className="block text-sm font-medium text-gray-700 mb-1">
        {label}
        {required && <span className="text-red-500 ml-0.5">*</span>}
      </label>

      {contactPersonsQuery.isLoading ? (
        <p className="text-xs text-gray-400">Loading contact...</p>
      ) : firstContact ? (
        <div className="flex items-center justify-between gap-3 rounded-lg border border-gray-200 bg-gray-50 px-4 py-3">
          <div>
            <p className="text-sm font-medium text-gray-800">
              {firstContact.displayName}
            </p>
            <div className="flex items-center gap-3 mt-0.5 text-xs text-gray-500">
              {firstContact.email && (
                <span className="flex items-center gap-1">
                  <Mail className="h-3.5 w-3.5" /> {firstContact.email}
                </span>
              )}
              {firstContact.phone && (
                <span className="flex items-center gap-1">
                  <Phone className="h-3.5 w-3.5" /> {firstContact.phone}
                </span>
              )}
            </div>
          </div>
        </div>
      ) : (
        <div className="flex items-center justify-between gap-3 rounded-[10px] border border-[#E5E5E5] bg-white px-4 py-3 shadow-[0_1px_3px_0_rgba(0,0,0,0.10)]">
          <div className="flex items-start gap-3">
            <TriangleAlert className="h-4 w-4 shrink-0 text-amber-600 mt-0.5" />
            <div className="flex flex-col gap-0.5">
              <p className="text-sm font-semibold text-amber-700">
                No Contact Person
              </p>
              <p className="text-sm text-gray-600">
                This funding company has no contact on file. Add one to
                continue.
              </p>
            </div>
          </div>
          <Button
            type="button"
            variant="secondary"
            rightIcon="userPlus"
            onClick={() => setShowAdd(true)}
            className="shrink-0"
          >
            Add Contact
          </Button>
        </div>
      )}

      {showAdd && fundingCompanyType?.id && (
        <ContactPersonFormModal
          open
          title="Add Contact Person"
          companyId={companyId}
          companyName={companyName}
          companyTypeId={fundingCompanyType.id}
          onClose={() => setShowAdd(false)}
          onSaved={(contact) => {
            onChange(contact.id, contact);
            setShowAdd(false);
          }}
        />
      )}
    </div>
  );
}
