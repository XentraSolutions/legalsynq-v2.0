"use client";

import { useEffect, useState } from "react";
import { ConfirmDialog } from "@/components/selling/modal";
import { AddSubContactModal } from "@/components/lien/add-subcontact-modal";
import { lookupApi } from "@/lib/lookup/lookup.api";
import type { LookupData } from "@/lib/lookup/lookup.types";
import type { ContactDetail } from "@/lib/contacts";

const FACILITY_CONTACT_SUBTYPE = "FacilityContactPerson";

/**
 * Selling-only counterpart to add-contact-staff-prompt.tsx's
 * contactSupportsStaffPrompt — applies to every top-level contact
 * (anything without a contactSubtype), not just LawFirm/MedicalFacility.
 * The Lien side intentionally keeps using the original, narrower check.
 */
export function contactSupportsContactPersonPrompt(
  contact: ContactDetail,
): boolean {
  return !contact.contactSubtype;
}

interface AddContactPersonPromptProps {
  /** The just-created parent contact, or null/undefined to keep the prompt closed. */
  contact: ContactDetail | null;
  onClose: () => void;
  /** Overrides the default bg-primary styling on the confirm/save buttons (selling's orange brand). */
  primaryButtonClassName?: string;
}

/**
 * Shown right after a new contact is created, asking whether to
 * immediately add a contact person associated with it.
 *
 * LawFirm and MedicalFacility reuse their existing, real sub-contact
 * flows (legal contacts / facility staff) exactly like the Lien side.
 * Every other contact type (Provider, FundingCompany, Lead, ...) falls
 * back to a generic "Contact Person" flow.
 *
 * TODO(backend): the generic fallback depends on a parent-contact link
 * the API doesn't support yet — see ContactPersonSection and
 * CreateContactRequestDto.parentContactId. It's wired up ahead of that
 * change so the UI is ready the moment the backend catches up.
 */
export function AddContactPersonPrompt({
  contact,
  onClose,
  primaryButtonClassName,
}: AddContactPersonPromptProps) {
  const [step, setStep] = useState<"confirm" | "form">("confirm");
  const [roles, setRoles] = useState<LookupData[]>([]);

  const isLawFirm = contact?.contactType === "LawFirm";
  const isMedicalFacility = contact?.contactType === "MedicalFacility";
  const usesRoles = !isMedicalFacility;

  useEffect(() => {
    if (contact) setStep("confirm");
  }, [contact]);

  useEffect(() => {
    if (!usesRoles) return;
    lookupApi.getContactRoles().then((res) => {
      setRoles(Array.isArray(res.data) ? res.data : []);
    });
  }, [usesRoles]);

  if (!contact || !contactSupportsContactPersonPrompt(contact)) return null;

  return (
    <>
      <ConfirmDialog
        open={step === "confirm"}
        onClose={onClose}
        onConfirm={() => setStep("form")}
        title="Add Contact Person?"
        description="The new contact has been added. Would you like to add a contact person associated with this contact? You can always do this later."
        confirmLabel="Yes"
        cancelLabel="Maybe Later"
        primaryButtonClassName={primaryButtonClassName}
      />

      {step === "form" && (
        <AddSubContactModal
          open
          onClose={onClose}
          title={
            isLawFirm
              ? "Add Legal Contact"
              : isMedicalFacility
                ? "Add New Medical Facility Staff"
                : "Add Contact Person"
          }
          contactType={contact.contactType}
          contactSubtype={isMedicalFacility ? FACILITY_CONTACT_SUBTYPE : undefined}
          lawFirmId={isLawFirm ? contact.id : undefined}
          facilityId={isMedicalFacility ? contact.id : undefined}
          parentContactId={
            !isLawFirm && !isMedicalFacility ? contact.id : undefined
          }
          roleOptions={usesRoles ? roles : undefined}
          allowCreateRole={usesRoles}
          onRoleCreated={(role) => setRoles((prev) => [...prev, role])}
          primaryButtonClassName={primaryButtonClassName}
          onSaved={onClose}
        />
      )}
    </>
  );
}
